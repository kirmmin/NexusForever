using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Contact.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientContactsRequestAdd)]
    public class ClientContactsRequestAdd : IReadable
    {
        public string PlayerName { get; private set; }
        public string Unknown0 { get; private set; }
        public ContactType Type { get; private set; }
        public string Message { get; private set; }

        public void Read(GamePacketReader reader)
        {
            PlayerName  = reader.ReadWideString();
            Unknown0  = reader.ReadWideString();
            Type  = (ContactType)reader.ReadByte(4);
            Message  = reader.ReadWideString();
        }
    }
}
