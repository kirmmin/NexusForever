using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientDuelDecline)]
    public class ClientDuelDecline : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
