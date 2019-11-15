using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerDuelCancelWarning)]
    public class ServerDuelCancelWarning : IWritable
    {
        public void Write(GamePacketWriter writer)
        {
        }
    }
}
