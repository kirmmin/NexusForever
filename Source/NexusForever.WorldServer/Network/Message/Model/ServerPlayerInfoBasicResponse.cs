using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Reputation.Static;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerPlayerInfoBasicResponse)]
    public class ServerPlayerInfoBasicResponse : IWritable
    {
        public byte ResultCode { get; set; }
        public TargetPlayerIdentity Identity { get; set; }
        public string Name { get; set; }
        public Faction Faction { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ResultCode, 3u);
            Identity.Write(writer);
            writer.WriteStringFixed(Name);
            writer.Write(Faction, 14u);
        }
    }
}
