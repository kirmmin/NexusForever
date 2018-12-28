using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerPathZoneEpisode)]
    public class ServerPathZoneEpisode : IWritable
    {
        public ushort ZoneId { get; set; }
        public ushort EpisodeId { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ZoneId, 15);
            writer.Write(EpisodeId, 14);
        }
    }
}
