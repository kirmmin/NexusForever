using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PathContent.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerPathEpisodeInit)]
    public class ServerPathEpisodeInit : IWritable
    {
        public class Mission : IWritable
        {
            public ushort MissionId { get; set; }
            public bool Completed { get; set; }
            public uint Userdata { get; set; }
            public uint Statedata { get; set; }

            public void Write(GamePacketWriter writer)
            {
                writer.Write(MissionId, 15u);
                writer.Write(Completed);
                writer.Write(Userdata);
                writer.Write(Statedata);
            }
        }

        public ushort EpisodeId { get; set; }
        public List<Mission> Missions { get; set; } = new List<Mission>();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(EpisodeId, 14u);
            writer.Write(Missions.Count, 16u);
            Missions.ForEach(e => e.Write(writer));
        }
    }
}
