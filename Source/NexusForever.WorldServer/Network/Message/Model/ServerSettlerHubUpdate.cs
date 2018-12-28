using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerSettlerHubUpdate)]
    public class ServerSettlerHubUpdate : IWritable
    {
        public ushort HubId { get; set; } // 14
        public uint[] Unknown0 { get; set; } = new uint[3];

        public void Write(GamePacketWriter writer)
        {
            writer.Write(HubId, 14u);

            for (int i = 0; i < Unknown0.Length; i++)
                writer.Write(Unknown0[i], 32u);
        }
    }
}
