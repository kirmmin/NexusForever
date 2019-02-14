using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Contact.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsUpdateType)]
    public class ServerContactsUpdateType : IWritable
    {
        public ulong ContactId { get; set; }
        public ContactType Type { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ContactId);
            writer.Write(Type, 4u);
        }
    }
}
