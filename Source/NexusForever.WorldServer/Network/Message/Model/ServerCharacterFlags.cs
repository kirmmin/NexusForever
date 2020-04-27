using NexusForever.Shared.IO.Map;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerCharacterFlagsUpdated)]
    public class ServerCharacterFlagsUpdated : IWritable
    {
        public uint Flags { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Flags);
        }
    }
}
