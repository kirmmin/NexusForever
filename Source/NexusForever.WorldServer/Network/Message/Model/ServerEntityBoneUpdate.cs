using System.Collections.Generic;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerEntityBoneUpdate)]
    public class ServerEntityBoneUpdate : IWritable
    {
        public uint UnitId { get; set; }
        public List<float> Bones { get; set; } = new List<float>();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(Bones.Count);
            Bones.ForEach(u => writer.Write(u));
        }
    }
}
