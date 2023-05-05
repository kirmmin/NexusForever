using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientGachaRollRequest)]
    public class ClientGachaRollRequest : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
