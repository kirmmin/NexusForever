using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientDuelAccept)]
    public class ClientDuelAccept : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
