using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerSettlerHubStatus)]
    public class ServerSettlerHubStatus : IWritable
    {
        public ushort HubId { get; set; } // 14u
        public List<SettlerImprovementGroupStatus> ImprovementGroups { get; set; } = new List<SettlerImprovementGroupStatus>();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(HubId, 14u);
            writer.Write(ImprovementGroups.Count);
            ImprovementGroups.ForEach(group => group.Write(writer));
        }
    }
}
