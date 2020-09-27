using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Social.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{

    [Message(GameMessageOpcode.ServerContactsSetPresence)]
    public class ServerContactsSetPresence : IWritable
    {
        public uint AccountId { get; set; }
        public ChatPresenceState Presence { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(AccountId);
            writer.Write(Presence, 3);
        }
    }
}
