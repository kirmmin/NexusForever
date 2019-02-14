using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientContactsSetNote)]
    public class ClientContactsSetNote : IReadable
    {
        public TargetPlayerIdentity PlayerIdentity { get; private set; } = new TargetPlayerIdentity();
        public string Note { get; private set; }

        public void Read(GamePacketReader reader)
        {
            PlayerIdentity.Read(reader);
            Note = reader.ReadWideString();
        }
    }
}
