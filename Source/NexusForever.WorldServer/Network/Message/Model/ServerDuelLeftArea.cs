using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerDuelLeftArea)]
    public class ServerDuelLeftArea : IWritable
    {
        public void Write(GamePacketWriter writer)
        {
        }
    }
}
