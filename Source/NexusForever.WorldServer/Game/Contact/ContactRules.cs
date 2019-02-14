using NexusForever.WorldServer.Game.Contact.Static;
using NexusForever.WorldServer.Network;
using System.Collections.Generic;
using System.Linq;

namespace NexusForever.WorldServer.Game.Contact
{
    partial class GlobalContactManager
    {
        /// <summary>
        /// Checks to see if the target character can become a contact
        /// </summary>
        public bool CanBeContact(WorldSession session, ulong recipientId, ContactType type, Dictionary</*guid*/ ulong, Contact> playerContacts)
        {
            return CanBeContactResult(session, recipientId, type, playerContacts) == ContactResult.Ok;
        }

        /// <summary>
        /// Checks to see if the target character can become a contact, and returns a <see cref="ContactResult"/> appropriately
        /// </summary>
        public ContactResult CanBeContactResult(WorldSession session, ulong receipientId, ContactType type, Dictionary</*guid*/ ulong, Contact> playerContacts)
        {
            Dictionary<ContactType, uint> maxTypeMap = new Dictionary<ContactType, uint>
            {
                { ContactType.Friend, GetMaxFriends() },
                { ContactType.Account, GetMaxFriends() },
                { ContactType.Ignore, GetMaxIgnored() },
                { ContactType.Rival, GetMaxRivals() }
            };
            Dictionary<ContactType, ContactResult> maxTypeResponseMap = new Dictionary<ContactType, ContactResult>
            {
                { ContactType.Friend, ContactResult.MaxFriends },
                { ContactType.Account, ContactResult.MaxFriends },
                { ContactType.Ignore, ContactResult.MaxIgnored },
                { ContactType.Rival, ContactResult.MaxRivals }
            };
            // Check player isn't capped for this Contact Type
            if (type != ContactType.FriendAndRival && playerContacts.Values.Where(c => c.Id == session.Player.CharacterId && c.Type == type && !c.IsPendingDelete).ToList().Count > maxTypeMap[type])
            {
                return maxTypeResponseMap[type];
            }
            else if (type == ContactType.FriendAndRival)
            {
                // Check both maximum counts are checked
                if (playerContacts.Values.Where(c => c.Type == ContactType.Friend && !c.IsPendingDelete).ToList().Count > maxTypeMap[ContactType.Friend])
                {
                    return maxTypeResponseMap[ContactType.Friend];
                }
                else if (playerContacts.Values.Where(c => c.Type == ContactType.Rival && !c.IsPendingDelete).ToList().Count > maxTypeMap[ContactType.Rival])
                {
                    return maxTypeResponseMap[ContactType.Rival];
                }
            }

            // Check recipient isn't already contact of requested type.
            if (playerContacts.Values.FirstOrDefault(c => c.ContactId == receipientId && c.Type == type && !c.IsPendingAcceptance && !c.IsPendingDelete) != null)
            {
                Dictionary<ContactType, ContactResult> alreadyContactResponseMap = new Dictionary<ContactType, ContactResult>
                {
                    { ContactType.Friend, ContactResult.PlayerAlreadyFriend },
                    { ContactType.Account, ContactResult.PlayerAlreadyFriend },
                    { ContactType.Ignore, ContactResult.PlayerAlreadyIgnored },
                    { ContactType.Rival, ContactResult.PlayerAlreadyRival }
                };

                return alreadyContactResponseMap[type];
            }

            // CHeck and make sure recipient doesn't have existing request
            if (type == ContactType.Friend || type == ContactType.FriendAndRival)
                if (playerContacts.Values.FirstOrDefault(c => c.ContactId == receipientId && c.Type == type && c.IsPendingAcceptance && !c.IsPendingDelete) != null)
                {
                    return ContactResult.PlayerQueuedRequests;
                }

            return ContactResult.Ok;
        }
    }
}
