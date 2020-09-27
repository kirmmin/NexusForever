using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsUpdateStatus)]
    public class ServerContactsUpdateStatus : IWritable
    {
        public TargetPlayerIdentity PlayerIdentity { get; set; }
        public float LastOnlineInDays { get; set; }

        public void Write(GamePacketWriter writer)
        {
            PlayerIdentity.Write(writer);
            writer.Write(LastOnlineInDays);
        }
    }
}
