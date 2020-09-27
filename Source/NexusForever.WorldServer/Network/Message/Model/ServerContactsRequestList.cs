using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Contact.Static;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsRequestList)]
    public class ServerContactsRequestList : IWritable
    {
        public class RequestData : IWritable
        {
            public ulong ContactId { get; set; }
            public TargetPlayerIdentity PlayerIdentity { get; set; }
            public ContactType ContactType { get; set; } // 3
            public float ExpiryInDays { get; set; }
            public string Message { get; set; }
            public string Name { get; set; }
            public Class Class { get; set; } // 14
            public Path Path { get; set; } // 3
            public byte Level { get; set; }

            public void Write(GamePacketWriter writer)
            {
                writer.Write(ContactId);
                PlayerIdentity.Write(writer);
                writer.Write(ContactType, 3u);
                writer.Write(ExpiryInDays);
                writer.WriteStringWide(Message);
                writer.WriteStringWide(Name);
                writer.Write(Class, 14u);
                writer.Write(Path, 3u);
                writer.Write(Level);
            }
        }

        public List<RequestData> ContactRequests { get; set; } = new List<RequestData>();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ContactRequests.Count, 16u);
            ContactRequests.ForEach(f => f.Write(writer));
        }
    }
}
