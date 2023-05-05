using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerGachaInit)]
    public class ServerGachaInit : IWritable
    {
        public class UnknownStruct0 : IWritable
        {
            public byte Unknown0 { get; set; } // 5
            public uint Unknown1 { get; set; }

            public void Write(GamePacketWriter writer)
            {
                writer.Write(Unknown0, 5u);
                writer.Write(Unknown1);
            }
        }

        /// <summary>
        /// Items are sent as Item2 IDs.
        /// </summary>
        public List<uint> Items { get; set; } = new();
        public List<UnknownStruct0> UnknownStruct0s { get; set; } = new();
        public List<uint> Unknown1 { get; set; } = new();
        public List<uint> Unknown2 { get; set; } = new();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Items.Count);
            Items.ForEach(i => writer.Write(i));

            writer.Write(UnknownStruct0s.Count);
            UnknownStruct0s.ForEach(i => i.Write(writer));

            writer.Write(Unknown1.Count);
            Unknown1.ForEach(i => writer.Write(i));

            writer.Write(Unknown2.Count);
            Unknown2.ForEach(i => writer.Write(i));
        }
    }
}
