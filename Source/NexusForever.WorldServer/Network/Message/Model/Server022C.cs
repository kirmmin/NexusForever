using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.Server022C)]
    public class Server022C : IWritable
    {
        public bool Unknown0 { get; set; }
        public float Unknown1 { get; set; }
        public float Unknown2 { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Unknown0);
            writer.Write(Unknown1);
            writer.Write(Unknown2);
        }
    }
}
