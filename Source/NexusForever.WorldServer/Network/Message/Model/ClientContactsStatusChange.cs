using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Social.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientContactsStatusChange)]
    public class ClientContactsStatusChange : IReadable
    {
        public ChatPresenceState Presence { get; set; }

        public void Read(GamePacketReader reader)
        {
            Presence = (ChatPresenceState)reader.ReadByte(3u);
        }
    }
}
