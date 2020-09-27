using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsSetNote)]
    public class ServerContactsSetNote : IWritable
    {
        public ulong ContactId { get; set; }
        public string Note { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ContactId);
            writer.WriteStringWide(Note);
        }
    }
}
