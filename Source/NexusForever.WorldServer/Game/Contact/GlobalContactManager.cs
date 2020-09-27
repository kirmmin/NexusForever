using NexusForever.WorldServer.Network;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusForever.Shared.Database;
using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.Network;
using NexusForever.Shared.Configuration;

namespace NexusForever.WorldServer.Game.Contact
{
    partial class GlobalContactManager : Singleton<GlobalContactManager>, IUpdate
    {
        /// <summary>
        /// Maximum amount of time a contact request can sit for before expiring
        /// </summary>
        public float GetMaxRequestDurationInDays() => ConfigurationManager<WorldServerConfiguration>.Instance.Config.Contacts.MaxRequestDuration ?? 7f;

        /// <summary>
        /// Id to be assigned to the next created contact.
        /// </summary>
        public ulong NextContactId => nextContactId++;
        private ulong nextContactId;

        private uint GetMaxFriends() => ConfigurationManager<WorldServerConfiguration>.Instance.Config.Contacts.MaxFriends ?? 100u;
        private uint GetMaxRivals() => ConfigurationManager<WorldServerConfiguration>.Instance.Config.Contacts.MaxRivals ?? 100u;
        private uint GetMaxIgnored() => ConfigurationManager<WorldServerConfiguration>.Instance.Config.Contacts.MaxIgnored ?? 100u;

        /// <summary>
        /// Minimum Id for the contact entry; Required to prevent the Client from marking the contact as Temporary
        /// </summary>
        private readonly ulong temporaryMod = 281474976710656;

        private readonly ConcurrentDictionary</*guid*/ ulong, Contact> contactsToSave = new ConcurrentDictionary<ulong, Contact>();
        private readonly ConcurrentDictionary</*guid*/ ulong, HashSet<ulong>> contactSubscriptions = new ConcurrentDictionary<ulong, HashSet<ulong>>();

        private readonly UpdateTimer saveTimer = new UpdateTimer(60d, true);

        /// <summary>
        /// Initialise the manager and run the start up tasks.
        /// </summary>
        public void Initialise()
        {
            // Note: This makes the first ID equal temporaryMod + 1.This is because the client needs a value with a minimum of 281474976710656 for the Contact ID otherwise it is flagged
            // as a temporary contact.
            // TODO: Fix this to also include temporary contacts?
            ulong maxDbId =  DatabaseManager.Instance.CharacterDatabase.GetNextContactId();
            nextContactId = maxDbId > temporaryMod ? maxDbId + 1ul : maxDbId + temporaryMod + 1ul;
        }

        /// <summary>
        /// Called in the main update method. Used to run tasks to sync <see cref="Contact"/>to database.
        /// </summary>
        /// <param name="lastTick"></param>
        public void Update(double lastTick)
        {
            saveTimer.Update(lastTick);

            if (saveTimer.HasElapsed)
            {
                var tasks = new List<Task>();

                foreach (Contact contact in contactsToSave.Values.ToList())
                    tasks.Add(DatabaseManager.Instance.CharacterDatabase.Save(contact.Save));

                Task.WaitAll(tasks.ToArray());

                contactsToSave.Clear();

                saveTimer.Reset();
            }
        }

        /// <summary>
        /// Get all <see cref="Contact"/> request responses which have been queued for save
        /// </summary>
        public IEnumerable<Contact> GetQueuedRequests(ulong ownerId)
        {
            foreach(Contact contact in contactsToSave.Values)
            {
                if (contact.OwnerId == ownerId)
                {
                    contactsToSave.TryRemove(contact.Id, out Contact contactRequest);
                    yield return contactRequest;
                }
            }
        }

        /// <summary>
        /// Declines the pending <see cref="Contact"/> request.
        /// </summary>
        public void DeclineRequest(Contact contact)
        {
            WorldSession contactSession = NetworkManager<WorldSession>.Instance.GetSession(s => s.Player?.CharacterId == contact.OwnerId);
            if (contactSession != null)
            {
                // Alert the User and have them update their own Contact
                contactSession.Player.ContactManager.ContactRequestDeclined(contact.Id);
            }
            else
            {
                // Player is offline, so we're going to save from the Global Contact Manager instead.
                contact.DeclineRequest();
                contactsToSave.TryAdd(contact.Id, contact);
            }    
        }

        public void AcceptRequest(Contact contact)
        {
            // Process Contact Request if user is online
            WorldSession targetSession = NetworkManager<WorldSession>.Instance.GetSession(s => s.Player?.CharacterId == contact.OwnerId);
            if (targetSession != null)
            {
                targetSession.Player.ContactManager.ContactRequestAccepted(contact.Id);
                NotifySubscriber(contact.OwnerId, contact.ContactId);
            }
            else
            {
                // Player is offline, so we're going to save from the Global Contact Manager instead.
                contact.AcceptRequest();
                contactsToSave.TryAdd(contact.Id, contact);
            }
        }

        /// <summary>
        /// Subscribe player with the provided Character ID to notifications if & when users in the Character ID List come online
        /// </summary>
        public void SubscribeTo(ulong characterId, IEnumerable<ulong> characterIdList)
        {
            if (contactSubscriptions.ContainsKey(characterId))
                contactSubscriptions[characterId].UnionWith(characterIdList);
            else
                contactSubscriptions.TryAdd(characterId, characterIdList.ToHashSet());
        }

        /// <summary>
        /// Unsubscribe player with the provided Character ID to notifications if & when users in the Character ID List come online
        /// </summary>
        public void UnsubscribeFrom(ulong characterId, List<ulong> characterIdList)
        {
            if (!contactSubscriptions.ContainsKey(characterId))
                throw new ArgumentOutOfRangeException($"Cannot unsubscribe from characters when the subscriber doesn't exist.");

            contactSubscriptions[characterId].RemoveWhere(i => characterIdList.Contains(i));
        }

        /// <summary>
        /// Remove subscriber with the provided Character ID from all previous subscriptions
        /// </summary>
        /// <param name="characterId"></param>
        public void RemoveSubscriber(ulong characterId)
        {
            if (!contactSubscriptions.ContainsKey(characterId))
                throw new ArgumentOutOfRangeException($"Cannot unsubscribe from characters when the subscriber doesn't exist.");

            contactSubscriptions.Remove(characterId, out _);
        }

        /// <summary>
        /// Notifies online <see cref="Contact"/> owners, that the player has logged in or out.
        /// </summary>
        public void NotifySubscribers(ulong characterId, bool loggingOut = false)
        {
            foreach ((ulong subscriberId, HashSet<ulong> subscriptions) in contactSubscriptions.Where(i => i.Value.Contains(characterId)))
                NotifySubscriber(subscriberId, characterId, loggingOut);
        }

        /// <summary>
        /// Notifies online <see cref="Contact"/> owners, that the player has logged in or out.
        /// </summary>
        public void NotifySubscriber(ulong subscriberId, ulong contactCharacterId, bool loggingOut = false)
        {
            WorldSession contactSession = NetworkManager<WorldSession>.Instance.GetSession(s => s.Player?.CharacterId == subscriberId);
            if (contactSession != null)
                contactSession.EnqueueMessageEncrypted(new ServerContactsUpdateStatus
                {
                    PlayerIdentity = new TargetPlayerIdentity
                    {
                        RealmId = WorldServer.RealmId,
                        CharacterId = contactCharacterId
                    },
                    LastOnlineInDays = loggingOut ? 0.00069f : 0
                });
        }

        /// <summary>
        /// Attempt to send a <see cref="Contact.Contact"/> request to it's target Player
        /// </summary>
        /// <param name="contactRequest">The Contact request you wish to send</param>
        public void SendRequestToPlayer(Contact contactRequest)
        {
            // Process Pending Request if user is online
            WorldSession targetSession = NetworkManager<WorldSession>.Instance.GetSession(s => s.Player?.CharacterId == contactRequest.ContactId);
            if (targetSession != null)
                targetSession.Player.ContactManager.ContactRequestCreate(contactRequest);
        }

        /// <summary>
        /// Remove a pending <see cref="Contact"/> request from an online player
        /// </summary>
        /// <param name="contactRequest">Contact request to remove</param>
        public void TryRemoveRequestFromOnlineUser(Contact contactRequest)
        {
            WorldSession targetSession = NetworkManager<WorldSession>.Instance.GetSession(s => s.Player?.CharacterId == contactRequest.ContactId);
            if (targetSession != null)
                SendContactRequestRemove(targetSession, contactRequest.Id);
        }

        /// <summary>
        /// Send a contact request removal packet to a player
        /// </summary>
        /// <param name="session">Session to send the contact request removal packet to</param>
        /// <param name="contactId">Contact ID of the request to be removed</param>
        private static void SendContactRequestRemove(WorldSession session, ulong contactId)
        {
            session.EnqueueMessageEncrypted(new ServerContactsRequestRemove
            {
                ContactId = contactId
            });
        }
    }
}
