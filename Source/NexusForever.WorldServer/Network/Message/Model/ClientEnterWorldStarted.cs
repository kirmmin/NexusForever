using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientEnterWorldStarted)]
    public class ClientEnterWorldStarted : IReadable
    {
        public uint Unknown0 { get; set; }
        public uint Unknown1 { get; set; }

        public void Read(GamePacketReader reader)
        {
            Unknown0 = reader.ReadUInt();
            Unknown1 = reader.ReadUInt();
        }
    }
}
