using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerDuelEnd)]
    public class ServerDuelEnd : IWritable
    {
        public uint WinnerId { get; set; }
        public uint LoserId { get; set; }
        public byte Result { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(WinnerId);
            writer.Write(LoserId);
            writer.Write(Result, 3u);
        }
    }
}
