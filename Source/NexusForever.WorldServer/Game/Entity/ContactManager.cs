using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusForever.Shared.Database;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.WorldServer.Game.CharacterCache;
using NexusForever.WorldServer.Game.Contact;
using NexusForever.WorldServer.Game.Contact.Static;
using NexusForever.WorldServer.Game.Social.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System.Collections;

namespace NexusForever.WorldServer.Game.Entity
{
    public class ContactManager : IEnumerable<Contact.Contact>, ISaveCharacter
    {
        private readonly Player player;

        private readonly Dictionary</*guid*/ ulong, Contact.Contact> contacts = new Dictionary<ulong, Contact.Contact>();
        private readonly HashSet<Contact.Contact> deletedContacts = new HashSet<Contact.Contact>();
        private readonly Dictionary</*guid*/ ulong, Contact.Contact> pendingRequests = new Dictionary<ulong, Contact.Contact>();

        /// <summary>
        /// Initialise a new <see cref="ContactManager"/> for a given <see cref="Player"/>
        /// </summary>
        public ContactManager(Player player, CharacterModel model)
        {
            this.player = player;

            foreach (CharacterContactModel contactEntry in model.Contact)
                contacts.TryAdd(contactEntry.Id, new Contact.Contact(contactEntry));

            // This gets all Request responses from the Global Manager. This is necessary for the 60s period where the Requester logs in after a Recipient has responded to their invite, and the Contact hasn't been Saved to the DB.
            foreach (Contact.Contact contact in GlobalContactManager.Instance.GetQueuedRequests(player.CharacterId))
            {
                if (contact.IsPendingDelete)
                    deletedContacts.Add(contact);
                else if (contacts.ContainsKey(contact.Id))
                    contacts[contact.Id] = contact;
                else
                    contacts.TryAdd(contact.Id, contact);
            }

            GlobalContactManager.Instance.SubscribeTo(player.CharacterId, contacts.Where(i => !i.Value.IsPendingAcceptance && (i.Value.Type == ContactType.Friend || i.Value.Type == ContactType.FriendAndRival)).Select(s => s.Value.ContactId));
        }

        private async Task BuildAndSendPendingRequests()
        {
            List<CharacterContactModel> pendingDBRequests = await DatabaseManager.Instance.CharacterDatabase.GetPendingContactRequests(player.CharacterId);
            foreach (CharacterContactModel request in pendingDBRequests)
                pendingRequests.TryAdd(request.Id, new Contact.Contact(request));

            SendPendingRequests();
        }

        /// <summary>
        /// Save all <see cref="Contact.Contact"/> for this <see cref="Player"/>
        /// </summary>
        public void Save(CharacterContext context)
        {
            foreach (Contact.Contact contact in deletedContacts)
                contact.Save(context);

            deletedContacts.Clear();

            foreach (Contact.Contact contact in contacts.Values)
            {
                if (contact.IsPendingAcceptance && contact.IsPendingCreate)
                    GlobalContactManager.Instance.SendRequestToPlayer(contact);

                contact.Save(context);
            }
        }

        /// <summary>
        /// Create a <see cref="Contact"/> request with another character
        /// </summary>
        /// <param name="session">Session making the request</param>
        /// <param name="recipientId">Character ID of the requested character</param>
        /// <param name="requestType">Type of Contact request to be made</param>
        /// <param name="message">Message to send to the recipient</param>
        public void CreateFriendRequest(ulong recipientId, string message)
        {
            ContactType requestType = ContactType.Friend;

            // Check rules.
            ContactResult result = GlobalContactManager.Instance.CanBeContactResult(player.Session, recipientId, requestType, contacts);
            if (result == ContactResult.Ok)
            {
                if (GetExistingContactByRecipient(recipientId, out Contact.Contact existingContact))
                {
                    if (existingContact.Type == ContactType.Rival || existingContact.Type == ContactType.Ignore)
                    {
                        existingContact.MakePendingAcceptance();
                        existingContact.InviteMessage = message;
                        existingContact.RequestTime = DateTime.Now;
                        GlobalContactManager.Instance.SendRequestToPlayer(existingContact);

                        result = ContactResult.RequestSent;
                    }
                }
                else
                {
                    // Save Pending Request.
                    Contact.Contact contactRequest = new Contact.Contact(GlobalContactManager.Instance.NextContactId, player.CharacterId, recipientId, message, requestType, true);
                    contacts.TryAdd(contactRequest.Id, contactRequest);
                    GlobalContactManager.Instance.SendRequestToPlayer(contactRequest);
                    result = ContactResult.RequestSent;
                }

                // Send Result to Requester
                if (result != ContactResult.Ok)
                    SendContactsResult(ContactResult.RequestSent);
            }
            else
                SendContactsResult(result);
        }

        /// <summary>
        /// Decline a <see cref="Contact"/> request
        /// </summary>
        /// <param name="contactId">Contact.ID of the declined request</param>
        /// <param name="addIgnore">Flag to create an ignore entry for this user</param>
        public void DeclineFriendRequest(ulong contactId, bool addIgnore = false)
        {
            if (!pendingRequests.TryGetValue(contactId, out Contact.Contact contactRequest))
                throw new Exception($"Contact Request with ID {contactId} not found");

            pendingRequests.Remove(contactId);
            GlobalContactManager.Instance.DeclineRequest(contactRequest);

            SendContactRequestRemove(contactId);

            if (addIgnore)
                CreateIgnored(contactRequest.ContactId);
        }

        /// <summary>
        /// Accept a <see cref="Contact.Contact"/> request
        /// </summary>
        /// <param name="id">ID of the accepted request</param>
        /// <param name="returnRequest">True if the player is making the requester a friend as well</param>
        public void AcceptFriendRequest(ulong id, bool returnRequest = false)
        {
            if (!pendingRequests.TryGetValue(id, out Contact.Contact contactRequest))
                throw new Exception($"Contact Request with ID {id} not found");

            // Ensure Contact can be accepted
            if (contactRequest.IsPendingDelete || !contactRequest.IsPendingAcceptance)
            {
                SendContactsResult(ContactResult.UnableToProcess);
                SendContactRequestRemove(contactRequest.Id);
                return;
            }

            // Remove the pending request from the receipient
            SendContactRequestRemove(contactRequest.Id);

            if (GetExistingContactByRecipient(contactRequest.OwnerId, out Contact.Contact existingRequest))
            {
                if (existingRequest.IsPendingAcceptance)
                {
                    UpdateExistingContact(existingRequest);
                    SendNewContact(GetContactData(existingRequest));
                    GlobalContactManager.Instance.TryRemoveRequestFromOnlineUser(existingRequest);
                    GlobalContactManager.Instance.SubscribeTo(player.CharacterId, new List<ulong> { existingRequest.ContactId });
                    GlobalContactManager.Instance.NotifySubscriber(player.CharacterId, existingRequest.ContactId);
                }
            }
            else if (returnRequest) 
            {
                if (GlobalContactManager.Instance.CanBeContact(player.Session, contactRequest.OwnerId, contactRequest.Type, contacts))
                {
                    ForceCreateContact(contactRequest.OwnerId, contactRequest.Type);
                    GlobalContactManager.Instance.SubscribeTo(player.CharacterId, new List<ulong> { contactRequest.OwnerId });
                    GlobalContactManager.Instance.NotifySubscriber(player.CharacterId, contactRequest.OwnerId);
                }
            }

            GlobalContactManager.Instance.AcceptRequest(contactRequest);
            pendingRequests.Remove(contactRequest.Id);
        }

        /// <summary>
        /// Create a <see cref="Contact"/> and force it to an accepted state
        /// </summary>
        /// <param name="session">Session of the player who will be the Owner of the contact</param>
        /// <param name="recipientId">Character ID that will be the contact</param>
        /// <param name="type">Type of contact</param>
        public void ForceCreateContact(ulong recipientId, ContactType type)
        {
            Contact.Contact newContact = new Contact.Contact(GlobalContactManager.Instance.NextContactId, player.CharacterId, recipientId, "", type, false);
            contacts.TryAdd(newContact.Id, newContact);

            SendNewContact(GetContactData(newContact));
        }

        /// <summary>
        /// Return a <see cref="Contact"/>, if there is one, that has the same owner and target player, but of a different <see cref="ContactType"/>
        /// </summary>
        /// <param name="recipientId">Character ID of the target player</param>
        /// <returns></returns>
        private bool GetExistingContact(ulong contactId, out Contact.Contact contact)
        {
            contact = contacts.Values.FirstOrDefault(c => c.Id == contactId && !c.IsPendingDelete);
            return contact != null;
        }

        /// <summary>
        /// Return a <see cref="Contact"/>, if there is one, that has the same owner and target player, but of a different <see cref="ContactType"/>
        /// </summary>
        /// <param name="recipientId">Character ID of the target player</param>
        /// <returns></returns>
        private bool GetExistingContactByRecipient(ulong recipientId, out Contact.Contact contact)
        {
            contact = contacts.Values.FirstOrDefault(c => c.ContactId == recipientId && !c.IsPendingDelete);
            return contact != null;
        }

        private void UpdateExistingContact(Contact.Contact contact)
        {
            // Change request to correct type and accept
            if (contact.Type == ContactType.Friend)
                contact.AcceptRequest();
            else if (contact.Type == ContactType.Rival)
            {
                contact.AcceptRequest();
                ChangeType(contact, ContactType.Friend, true);
            }
            else if (contact.Type == ContactType.Ignore)
            {
                contact.AcceptRequest();
                ChangeType(contact, ContactType.Friend, true);
            }
        }

        private void ChangeType(Contact.Contact contact, ContactType newType, bool forceNewType = false)
        {
            if (forceNewType)
                contact.Type = newType;
            else
                switch (contact.Type)
                {
                    case ContactType.Rival:
                        if (newType == ContactType.Friend)
                            contact.Type = ContactType.FriendAndRival;
                        else if (newType == ContactType.Ignore)
                            contact.Type = ContactType.Ignore;
                        break;
                    case ContactType.Friend:
                        if (newType == ContactType.Rival)
                            contact.Type = ContactType.FriendAndRival;
                        else if (newType == ContactType.Ignore)
                        {
                            contact.Type = ContactType.Ignore;
                            GlobalContactManager.Instance.UnsubscribeFrom(player.CharacterId, new List<ulong> { contact.ContactId });
                        }
                        break;
                    case ContactType.Ignore:
                        if (newType == ContactType.Rival)
                            contact.Type = ContactType.Rival;
                        else
                            contact.Type = ContactType.Ignore;
                        break;
                    case ContactType.FriendAndRival:
                        contact.Type = newType;
                        break;
                }

            SendContactsUpdateType(contact); 
        }

        /// <summary>
        /// Calculate the expiry time, as a float, for this <see cref="Contact"/> request
        /// </summary>
        /// <param name="contact">The contact to calculate the expiry for</param>
        /// <param name="expiryTime">The expiry time will be returned via the out parameter</param>
        private void CalculateExpiryTime(Contact.Contact contact, out float expiryTime)
        {
            expiryTime = GlobalContactManager.Instance.GetMaxRequestDurationInDays() - (float)Math.Abs(contact.RequestTime.Subtract(DateTime.UtcNow).TotalDays);
        }

        /// <summary>
        /// Create a <see cref="ContactData"/> from a <see cref="Contact"/>
        /// </summary>
        /// <param name="contact">Contact to build a ContactData from</param>
        /// <returns></returns>
        private ContactData GetContactData(Contact.Contact contact)
        {
            return new ContactData
            {
                ContactId = contact.Id,
                PlayerIdentity = new TargetPlayerIdentity
                {
                    RealmId = WorldServer.RealmId,
                    CharacterId = contact.ContactId
                },
                Note = contact.PrivateNote,
                Type = contact.Type
            };
        }

        /// <summary>
        /// Called by <see cref="GlobalContactManager"/> when a contact request is made
        /// </summary>
        public void ContactRequestCreate(Contact.Contact contact)
        {
            // Request Received from another Player - Handle
            pendingRequests.TryAdd(contact.Id, contact);
            SendPendingRequests();
        }

        /// <summary>
        /// Called by <see cref="GlobalContactManager"/> when a request is accepted
        /// </summary>
        public void ContactRequestAccepted(ulong contactId)
        {
            GetExistingContact(contactId, out Contact.Contact contact);
            ChangeType(contact, ContactType.Friend);
            contact.AcceptRequest();
            SendNewContact(GetContactData(contact));
            GlobalContactManager.Instance.SubscribeTo(player.CharacterId, new List<ulong> { contact.ContactId });
        }

        /// <summary>
        /// Called by <see cref="GlobalContactManager"/> when a request is accepted
        /// </summary>
        public void ContactRequestDeclined(ulong contactId)
        {
            GetExistingContact(contactId, out Contact.Contact contact);
            contact.DeclineRequest();
            contacts.Remove(contact.Id);
            deletedContacts.Add(contact);
        }

        /// <summary>
        /// Create a rival <see cref="Contact.Contact"/>
        /// </summary>
        /// <param name="recipientId">Character ID of the player to make a rival of</param>
        public void CreateRival(ulong recipientId)
        {
            if (GlobalContactManager.Instance.CanBeContact(player.Session, recipientId, ContactType.Rival, contacts))
            {
                if (GetExistingContactByRecipient(recipientId, out Contact.Contact existingContact))
                    ChangeType(existingContact, ContactType.Rival);
                else
                    ForceCreateContact(recipientId, ContactType.Rival);
            }
        }

        /// <summary>
        /// Create an ignored <see cref="Contact"/>
        /// </summary>
        /// <param name="session">Session of a player making the ignore request</param>
        /// <param name="recipientId">Character ID of the player to ignore</param>
        public void CreateIgnored(ulong recipientId)
        {
            if (GlobalContactManager.Instance.CanBeContact(player.Session, recipientId, ContactType.Ignore, contacts))
            {
                if (GetExistingContactByRecipient(recipientId, out Contact.Contact existingContact))
                {
                    if (!existingContact.IsPendingAcceptance)
                    {
                        ChangeType(existingContact, ContactType.Ignore);
                        return;
                    }
                    else
                    {
                        DeleteContact(existingContact, existingContact.Type);

                        if (existingContact.IsPendingAcceptance)
                            GlobalContactManager.Instance.TryRemoveRequestFromOnlineUser(existingContact);
                    }
                }

                ForceCreateContact(recipientId, ContactType.Ignore);
                SendContactsResult(ContactResult.PlayerOnIgnored);
            }
            else
                SendContactsResult(ContactResult.UnableToProcess);
        }

        /// <summary>
        /// Delete <see cref="Contact"/> from a player's contacts
        /// </summary>
        /// <param name="session">Session of player making the deletion</param>
        /// <param name="playerIdentity"><see cref="TargetPlayerIdentity"/> of the player to delete</param>
        /// <param name="type">Type to confirm deletion with</param>
        public void DeleteContact(TargetPlayerIdentity playerIdentity, ContactType type)
        {
            Contact.Contact contactToDelete = contacts.Values.FirstOrDefault(s => s.ContactId == playerIdentity.CharacterId && !s.IsPendingDelete);
            if (contactToDelete == null)
                throw new Exception($"Contact matching realm {playerIdentity.RealmId} & characterId {playerIdentity.CharacterId} not found.");

            DeleteContact(contactToDelete, type);
        }

        /// <summary>
        /// Delete <see cref="Contact.Contact"/> from a player's contacts
        /// </summary>
        /// <param name="contactToDelete">Contact to be deleted</param>
        public void DeleteContact(Contact.Contact contactToDelete, ContactType requestedTypeToDelete)
        {
            switch (requestedTypeToDelete)
            {
                case ContactType.Friend:
                    if (contactToDelete.Type == ContactType.FriendAndRival)
                    {
                        ChangeType(contactToDelete, ContactType.Rival, true);
                        GlobalContactManager.Instance.UnsubscribeFrom(player.CharacterId, new List<ulong> { contactToDelete.ContactId });
                        return;
                    }
                    else
                    {
                        if (contactToDelete.IsPendingCreate)
                            contacts.Remove(contactToDelete.Id);
                        else
                        {
                            contactToDelete.EnqueueDelete();
                            GlobalContactManager.Instance.UnsubscribeFrom(player.CharacterId, new List<ulong> { contactToDelete.ContactId });
                            contacts.Remove(contactToDelete.Id);
                            deletedContacts.Add(contactToDelete);
                        }
                    }
                    break;
                case ContactType.Rival:
                    if (contactToDelete.Type == ContactType.FriendAndRival)
                    {
                        ChangeType(contactToDelete, ContactType.Friend, true);
                        return;
                    }
                    else
                    {
                        if (contactToDelete.IsPendingCreate)
                            contacts.Remove(contactToDelete.Id);
                        else
                        {
                            contactToDelete.EnqueueDelete();
                            contacts.Remove(contactToDelete.Id);
                            deletedContacts.Add(contactToDelete);
                        }    
                    }
                    break;
                case ContactType.Ignore:
                    if (contactToDelete.IsPendingAcceptance)
                        ChangeType(contactToDelete, ContactType.Friend, true);
                    else
                        if (contactToDelete.IsPendingCreate)
                            contacts.Remove(contactToDelete.Id);
                        else
                        {
                            contactToDelete.EnqueueDelete();
                            contacts.Remove(contactToDelete.Id);
                            deletedContacts.Add(contactToDelete);
                        }
                    break;
            }

            SendContactDelete(contactToDelete.Id);
        }

        /// <summary>
        /// Set a private note associated with a <see cref="Contact"/>
        /// </summary>
        /// <param name="playerIdentity">CharacterIdentity of the player's note to update</param>
        /// <param name="note">Note to set</param>
        public void SetPrivateNote(TargetPlayerIdentity playerIdentity, string note)
        {
            Contact.Contact contactToModify = contacts.Values.FirstOrDefault(s => s.ContactId == playerIdentity.CharacterId && !s.IsPendingDelete);
            if (contactToModify == null)
                throw new Exception($"Contact matching realm {playerIdentity.RealmId} & characterId {playerIdentity.CharacterId} not found.");

            SetPrivateNote(contactToModify, note);
        }

        /// <summary>
        /// Set a private note associated with a <see cref="Contact"/>
        /// </summary>
        /// <param name="contactToModify"></param>
        /// <param name="Note"></param>
        public void SetPrivateNote(Contact.Contact contactToModify, string Note)
        {
            contactToModify.PrivateNote = Note;

            player.Session.EnqueueMessageEncrypted(new ServerContactsSetNote
            {
                ContactId = contactToModify.Id,
                Note = contactToModify.PrivateNote
            });
        }

        /// <summary>
        /// Returns whether or not this Character ID is ignored.
        /// </summary>
        public bool IsIgnored(ulong characterId)
        {
            return contacts.Values.SingleOrDefault(c => c.ContactId == characterId && c.Type == ContactType.Ignore) != null;
        }

        /// <summary>
        /// Called by a <see cref="Player"/> when logging in
        /// </summary>
        /// <param name="session">Session of the player logging in</param>
        public void OnLogin()
        {
            SendPersonalStatus();
            SendAccountStatus();
            SendContactsListForPlayer();
            SendAccountList();
            Task.Run(() => {
                _ = BuildAndSendPendingRequests();
            });

            GlobalContactManager.Instance.NotifySubscribers(player.CharacterId);
        }


        /// <summary>
        /// Called by a <see cref="Player"/> when logging out
        /// </summary>
        /// <param name="session">Session of the player logging out</param>
        public void OnLogout()
        {
            GlobalContactManager.Instance.RemoveSubscriber(player.CharacterId);
            GlobalContactManager.Instance.NotifySubscribers(player.CharacterId, true);
        }

        /// <summary>
        /// Set a player's <see cref="ChatPresenceState"/>
        /// </summary>
        /// <param name="session">Session of a player to send the update to</param>
        private void SendPersonalStatus()
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsSetPresence
            {
                AccountId = player.Session.Account.Id,
                Presence = ChatPresenceState.Available
            });
        }

        /// <summary>
        /// Send the <see cref="ServerContactsAccountStatus"/> packet to a player
        /// </summary>
        /// <param name="session">Session of a player to receive the packet</param>
        private void SendAccountStatus()
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsAccountStatus
            {
                AccountPublicStatus = "",
                AccountNickname = "",
                Presence = ChatPresenceState.Available,
                BlockStrangerRequests = true
            });
        }

        /// <summary>
        /// Send a player's <see cref="Contact"/> list to them
        /// </summary>
        private void SendContactsListForPlayer()
        {
            List<Contact.Contact> contactList = contacts.Values.Where(d => !d.IsPendingAcceptance && !d.IsPendingDelete).ToList();

            List<ContactData> contactDataList = BuildContactsListData(contactList);
            SendContactsList(contactDataList);
        }

        /// <summary>
        /// Creates a list of <see cref="ContactData"/> from a list of <see cref="Contact"/> to be used as part of a player's contact list
        /// </summary>
        /// <param name="contactList">List of Contacts to be converted</param>
        /// <returns></returns>
        private List<ContactData> BuildContactsListData(List<Contact.Contact> contactList)
        {
            List<ContactData> contactsList = new List<ContactData>();

            foreach (Contact.Contact contact in contactList)
                if (!(contact.Type == ContactType.Friend && contact.IsPendingAcceptance))
                    contactsList.Add(GetContactData(contact));

            return contactsList;
        }

        /// <summary>
        /// Send the <see cref="ServerContactsList"/> packet to a player
        /// </summary>
        /// <param name="contacts">List of contacts to be included</param>
        private void SendContactsList(List<ContactData> contacts)
        {
            var ContactsList = new ServerContactsList
            {
                Contacts = contacts
            };
            player.Session.EnqueueMessageEncrypted(ContactsList);
        }

        /// <summary>
        /// Send a list of account friends to a player
        /// </summary>
        /// <param name="session">Session of a player recieving the list</param>
        private void SendAccountList()
        {
            var serverFriendshipAccountList = new ServerContactsAccountList();
            player.Session.EnqueueMessageEncrypted(serverFriendshipAccountList);
        }

        /// <summary>
        /// Sends pending <see cref="Contact"/> requests to the player
        /// </summary>
        /// <param name="session">Session of a player </param>
        private void SendPendingRequests()
        {
            List<ServerContactsRequestList.RequestData> contactRequestDataList = new List<ServerContactsRequestList.RequestData>();

            List<Contact.Contact> contactRequestsToSend = new List<Contact.Contact>();

            foreach (Contact.Contact contactRequest in pendingRequests.Values)
            {
                CalculateExpiryTime(contactRequest, out float expiryTime);
                if (expiryTime > 0f)
                    contactRequestsToSend.Add(contactRequest);
                else
                {
                    pendingRequests.Remove(contactRequest.Id);
                    GlobalContactManager.Instance.DeclineRequest(contactRequest);
                }
                    
            }

            foreach (Contact.Contact pendingRequest in contactRequestsToSend)
            {
                ICharacter characterInfo = CharacterManager.Instance.GetCharacterInfo(pendingRequest.OwnerId);
                contactRequestDataList.Add(GetContactRequestData(pendingRequest, characterInfo));

                player.Session.EnqueueMessageEncrypted(new ServerContactsRequestList
                {
                    ContactRequests = contactRequestDataList
                });
            }
        }

        /// <summary>
        /// Create a <see cref="ServerContactsRequestList.RequestData"/> for a <see cref="ICharacter"/>
        /// </summary>
        /// <param name="contactRequest">The Contact instance</param>
        /// <param name="character">Character to build the request of</param>
        /// <returns></returns>
        private ServerContactsRequestList.RequestData GetContactRequestData(Contact.Contact contactRequest, ICharacter character)
        {
            CalculateExpiryTime(contactRequest, out float expiryTime);

            return new ServerContactsRequestList.RequestData
            {
                ContactId = contactRequest.Id,
                PlayerIdentity = new TargetPlayerIdentity
                {
                    RealmId = WorldServer.RealmId,
                    CharacterId = character.CharacterId
                },
                ContactType = ContactType.Friend,
                Message = contactRequest.InviteMessage,
                Name = character.Name,
                Class = character.Class,
                Path = character.Path,
                Level = (byte)character.Level,
                ExpiryInDays = expiryTime // TODO: Calculate expiry date and delete record if request expires. Currently set to 1 week.
            };
        }

        /// <summary>
        /// Send a Contacts Result packet to a player
        /// </summary>
        /// <param name="session">Session of a player receiving the results packet</param>
        /// <param name="result">Result code to be used</param>
        public void SendContactsResult(ContactResult result)
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsRequestResult
            {
                Unknown0 = "",
                Results = result
            });
        }

        /// <summary>
        /// Send a contact request removal packet to a player
        /// </summary>
        /// <param name="contactId">Contact ID of the request to be removed</param>
        private void SendContactRequestRemove(ulong guid)
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsRequestRemove
            {
                ContactId = guid
            });
        }

        /// <summary>
        /// Send a <see cref="ServerContactsAdd"/> packet for a new <see cref="Contact.Contact"/> to the player
        /// </summary>
        /// <param name="session">Session to send the contact to</param>
        /// <param name="contactData">ContactData to be sent</param>
        private void SendNewContact(ContactData contactData)
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsAdd
            {
                Contact = contactData
            });
        }

        /// <summary>
        /// Send a <see cref="ServerContactsDeleteResult"/> packet for a <see cref="Contact.Contact"/> to the player
        /// </summary>
        /// <param name="session">Session to send the contact delete packet to</param>
        /// <param name="contactId">Contact ID to be deleted</param>
        private void SendContactDelete(ulong contactId)
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsDeleteResult
            {
                ContactId = contactId
            });
        }

        /// <summary>
        /// Send a <see cref="ServerContactsUpdateType"/> packet for a <see cref="Contact.Contact"/> to the player
        /// </summary>
        /// <param name="contact"></param>
        private void SendContactsUpdateType(Contact.Contact contact)
        {
            player.Session.EnqueueMessageEncrypted(new ServerContactsUpdateType
            {
                ContactId = contact.Id,
                Type = contact.Type
            });
        }

        public IEnumerator<Contact.Contact> GetEnumerator()
        {
            return contacts.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
