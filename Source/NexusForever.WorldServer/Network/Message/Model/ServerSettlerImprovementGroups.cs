using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerSettlerImprovementGroups)]
    public class ServerSettlerImprovementGroups : IWritable
    {
        public uint UnitId { get; set; }
        public List<uint> Groups { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(Groups.Count);
            Groups.ForEach(g => writer.Write(g));
        }
    }
}
