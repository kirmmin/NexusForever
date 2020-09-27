using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Contact.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientContactsRequestResponse)]
    public class ClientContactsRequestResponse : IReadable
    {
        public ulong ContactId { get; private set; }
        public ContactResponse Response { get; private set; }

        public void Read(GamePacketReader reader)
        {
            ContactId  = reader.ReadULong(64u);
            Response  = (ContactResponse)reader.ReadByte(3u);
        }
    }
}
