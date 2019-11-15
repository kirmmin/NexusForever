using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientDuelRequest)]
    public class ClientDuelRequest : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
