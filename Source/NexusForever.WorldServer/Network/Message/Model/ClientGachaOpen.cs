using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientGachaOpen)]
    public class ClientGachaOpen : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
