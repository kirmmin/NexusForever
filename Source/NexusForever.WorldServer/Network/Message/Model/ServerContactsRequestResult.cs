using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Contact.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsRequestResult)]
    public class ServerContactsRequestResult : IWritable
    {
        public string Unknown0 { get; set; }
        public ContactResult Results { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.WriteStringWide(Unknown0);
            writer.Write(Results, 6);
        }
    }
}
