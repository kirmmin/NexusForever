using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientSettlerBuildImprovement)]
    public class ClientSettlerBuildImprovement : IReadable
    {
        public ushort GroupId { get; private set; } // 14
        public int Tier { get; private set; }

        public void Read(GamePacketReader reader)
        {
            GroupId  = reader.ReadUShort(14u);
            Tier = reader.ReadInt();
        }
    }
}
