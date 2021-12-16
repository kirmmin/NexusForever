using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.Server088C)]
    public class Server088C : IWritable
    {
        public uint UnitId { get; set; }
        public bool Unknown0 { get; set; }
        public byte Unknown1 { get; set; } // 5u
        public uint Unknown2 { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(Unknown0);
            writer.Write(Unknown1, 5u);
            writer.Write(Unknown2);
        }
    }
}
