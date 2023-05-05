using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerGachaRollResult)]
    public class ServerGachaRollResult : IWritable
    {
        public byte Unknown0 { get; set; } // 3 - Seems to always be 3 after a roll attempt, could be Result
        public byte Unknown1 { get; set; } // 2
        public byte WinType { get; set; } // 2 - 1 for "Normal", 2 for "Carrot Charged Win"
        public byte Unknown3 { get; set; } // 2
        public uint[] AccountItemsWon { get; set; } = new uint[3]; // 3 x 32
        public bool[] ItemsClaimed { get; set; } = new bool[3]; // 3 x 32 - possibly a boolean indicating whether the account item in that slot has been opened

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Unknown0, 3u);
            writer.Write(Unknown1, 2u);
            writer.Write(WinType, 2u);
            writer.Write(Unknown3, 2u);

            for (int i = 0; i < AccountItemsWon.Length; i++)
                writer.Write(AccountItemsWon[i]);

            for (int i = 0; i < ItemsClaimed.Length; i++)
                writer.Write(ItemsClaimed[i]);
        }
    }
}
