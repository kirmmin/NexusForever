using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerEntityTargetUnit)]
    public class ServerEntityTargetUnit : IWritable
    {
        public uint UnitId { get; set; }
        public uint NewTargetId { get; set; }
        public uint ThreatLevel { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(NewTargetId);
            writer.Write(ThreatLevel);
        }
    }
}
