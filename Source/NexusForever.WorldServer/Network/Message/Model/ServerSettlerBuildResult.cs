using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerSettlerBuildResult)]
    public class ServerSettlerBuildResult : IWritable
    {
        public uint Result { get; set; }
        public ushort ImprovementId { get; set; } // 15
        public ushort GroupId { get; set; } // 14

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Result);
            writer.Write(ImprovementId, 15u);
            writer.Write(GroupId, 14u);
        }
    }
}
