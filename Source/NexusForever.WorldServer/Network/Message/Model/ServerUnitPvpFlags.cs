using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerUnitPvpFlags)]
    public class ServerUnitPvpFlags : IWritable
    {
        public uint UnitId { get; set; }
        public PvPFlag PvpFlags { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(PvpFlags, 3u);
        }
    }
}
