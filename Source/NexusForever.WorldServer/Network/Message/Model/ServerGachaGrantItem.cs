using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerGachaGrantItem)]
    public class ServerGachaGrantItem : IWritable
    {
        public bool Unknown0 { get; set; } = true; // Always true
        public byte Unknown1 { get; set; } // 3 - Seems to always be 3 when claiming, until final item claimed
        public bool[] ItemsClaimed { get; set; } = new bool[3];

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Unknown0);
            writer.Write(Unknown1, 3u);

            for (int i = 0; i < ItemsClaimed.Length; i++)
                writer.Write(ItemsClaimed[i]);
        }
    }
}
