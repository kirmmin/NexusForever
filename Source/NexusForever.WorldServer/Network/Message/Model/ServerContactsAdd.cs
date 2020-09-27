using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsAdd)]
    public class ServerContactsAdd : IWritable
    {
        public ContactData Contact { get; set; } = new ContactData();

        public void Write(GamePacketWriter writer)
        {
            Contact.Write(writer);
        }
    }
}
