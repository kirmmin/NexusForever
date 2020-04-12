using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerEntityDestroy)]
    public class ServerEntityDestroy : IWritable
    {
        public uint Guid { get; set; }
        public bool WasDestroyed { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Guid);
            writer.Write(WasDestroyed);
        }
    }
}
