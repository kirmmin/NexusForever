using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Social;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientChatJoinCustom)]
    public class ClientChatJoinCustom : IReadable
    {
        public ChatChannel ChatChannel { get; private set; } // 14u
        public string Name { get; private set; }
        public uint Unknown2 { get; private set; }

        public void Read(GamePacketReader reader)
        {
            ChatChannel = reader.ReadEnum<ChatChannel>(14u);
            Name        = reader.ReadWideString();
            Unknown2    = reader.ReadUInt();
        }
    }
}
