using NexusForever.Shared.Network.Message;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Network.Message.Model.Shared
{
    public class SettlerImprovementGroupStatus : IWritable
    {
        public ushort GroupId { get; set; } // 14u
        public int Tier { get; set; } = -1;
        public uint RemainingMs { get; set; }
        public uint BundleCount { get; set; }

        public void Write(NexusForever.Shared.Network.GamePacketWriter writer)
        {
            writer.Write(GroupId, 14u);
            writer.Write(Tier);
            writer.Write(RemainingMs);
            writer.Write(BundleCount);
        }
    }
}
