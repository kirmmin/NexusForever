using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Combat.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerEntityCCStateSet)]
    public class ServerEntityCCStateSet : IWritable
    {
        public uint UnitId { get; set; }
        public CCState State { get; set; }
        public uint ServerUniqueEffectId { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(State, 5u);
            writer.Write(ServerUniqueEffectId);
        }
    }
}
