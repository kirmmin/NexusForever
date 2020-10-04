using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerThreatListUpdate)]
    public class ServerThreatListUpdate : IWritable
    {
        public uint SrcUnitId { get; set; }
        public uint[] ThreatUnitIds { get; set; } = new uint[5];
        public uint[] ThreatLevels { get; set; } = new uint[5];

        public void Write(GamePacketWriter writer)
        {
            writer.Write(SrcUnitId);

            for (int i = 0; i < ThreatUnitIds.Length; i++)
                writer.Write(ThreatUnitIds[i]);

            for (int i = 0; i < ThreatLevels.Length; i++)
                writer.Write(ThreatLevels[i]);
        }
    }
}
