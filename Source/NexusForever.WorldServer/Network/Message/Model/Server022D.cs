using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.Server022D)]
    public class Server022D : IWritable
    {
        public void Write(GamePacketWriter writer)
        {
        }
    }
}
