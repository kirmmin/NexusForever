using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerSettlerBuildStatus)]
    public class ServerSettlerBuildStatus : IWritable
    {
        public ushort HubId { get; set; } // 14u
        public SettlerImprovementGroupStatus ImprovementGroup { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(HubId, 14u);
            ImprovementGroup.Write(writer);
        }
    }
}
