using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity;
using System.Numerics;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientHousingPropUpdate)]
    public class ClientHousingPropUpdate : IReadable
    {
        public ushort RealmId { get; private set; } // 14u
        public ulong ResidenceId { get; private set; }
        public long PropId { get; private set; }
        public uint DecorId { get; private set; }
        public byte Operation { get; private set; } // 3u
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }

        public void Read(GamePacketReader reader)
        {
            RealmId  = reader.ReadUShort(14u);
            ResidenceId = reader.ReadULong();
            PropId    = reader.ReadLong();
            DecorId   = reader.ReadUInt();
            Operation = reader.ReadByte(3u);
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
