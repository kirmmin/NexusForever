using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientGachaClaimItem)]
    public class ClientFortuneClaimItem : IReadable
    {
        public uint Index { get; private set; }

        public void Read(GamePacketReader reader)
        {
            Index = reader.ReadUInt();
        }
    }
}
