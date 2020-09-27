using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Social.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{

    [Message(GameMessageOpcode.ServerContactsAccountStatus)]
    public class ServerContactsAccountStatus : IWritable
    {
        public string AccountPublicStatus { get; set; }
        public string AccountNickname { get; set; }
        public ChatPresenceState Presence { get; set; }
        public bool BlockStrangerRequests { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.WriteStringWide(AccountPublicStatus);
            writer.WriteStringWide(AccountNickname);
            writer.Write(Presence, 3);
            writer.Write(BlockStrangerRequests);
        }
    }
}
