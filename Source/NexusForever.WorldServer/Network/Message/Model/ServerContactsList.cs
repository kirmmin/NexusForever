using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Contact.Static;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerContactsList)]
    public class ServerContactsList : IWritable
    {
        public List<ContactData> Contacts { get; set; } = new List<ContactData>();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Contacts.Count, 16u);
            Contacts.ForEach(f => f.Write(writer));
        }
    }
}
