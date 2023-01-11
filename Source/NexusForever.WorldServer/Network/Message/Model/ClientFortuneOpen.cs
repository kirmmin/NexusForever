using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientFortuneOpen)]
    public class ClientFortuneOpen : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
