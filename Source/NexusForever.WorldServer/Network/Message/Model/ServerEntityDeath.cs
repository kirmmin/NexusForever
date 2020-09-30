using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerEntityDeath)]
    public class ServerEntityDeath : IWritable
    {
        public uint UnitId { get; set; }
        public bool Dead { get; set; }
        public byte Reason { get; set; }
        public uint RezHealth { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(Dead);
            writer.Write(Reason, 5u);
            writer.Write(RezHealth);
        }
    }
}