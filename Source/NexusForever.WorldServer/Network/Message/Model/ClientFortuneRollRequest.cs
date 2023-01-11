using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientFortuneRollRequest)]
    public class ClientFortuneRollRequest : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
