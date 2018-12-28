using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerSettlerImprovementInfo)]
    public class ServerSettlerImprovementInfo : IWritable
    {
        public class TierInfo : IWritable
        {
            public string Name { get; set; }
            public uint Tier { get; set; }

            public void Write(GamePacketWriter writer)
            {
                writer.WriteStringWide(Name);
                writer.Write(Tier);
            }
        }

        public uint UnitId { get; set; }
        public ushort GroupId { get; set; } // 14
        public uint RemainingMs { get; set; }
        public List<string> Owners { get; set; } = new List<string>();
        public List<TierInfo> Tiers { get; set; } = new List<TierInfo>();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(GroupId, 14u);
            writer.Write(RemainingMs);

            writer.Write(Owners.Count);
            Owners.ForEach(i => writer.WriteStringWide(i));

            writer.Write(Tiers.Count);
            Tiers.ForEach(t => t.Write(writer));
        }
    }
}
