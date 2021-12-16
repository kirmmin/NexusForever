using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.Client0721)]
    public class Client0721 : IReadable
    {

        public void Read(GamePacketReader reader)
        {
        }
    }
}
