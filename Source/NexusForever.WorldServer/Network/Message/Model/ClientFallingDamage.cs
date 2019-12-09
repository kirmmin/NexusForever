using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientFallingDamage)]
    public class ClientFallingDamage : IReadable
    {
        public float HealthPercent { get; private set; }

        public void Read(GamePacketReader reader)
        {
            HealthPercent = reader.ReadSingle();
        }
    }
}
