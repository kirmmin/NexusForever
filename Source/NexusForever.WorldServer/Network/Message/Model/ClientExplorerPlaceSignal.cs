using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientExplorerPlaceSignal)]
    public class ClientExplorerPlaceSignal : IReadable
    {
        public ushort MissionId { get; private set; }
        public uint NodeIndex { get; private set; }

        public void Read(GamePacketReader reader)
        {
            MissionId = reader.ReadUShort(15u);
            NodeIndex = reader.ReadUInt();
        }
    }
}
