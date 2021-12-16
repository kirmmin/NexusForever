using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity;
using System.Numerics;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientHousingEnterInside)]
    public class ClientHousingEnterInside : IReadable
    {
        public ushort RealmId { get; private set; } // 14u
        public ulong ResidenceId { get; private set; }

        public void Read(GamePacketReader reader)
        {
            RealmId  = reader.ReadUShort(14u);
            ResidenceId = reader.ReadULong();
        }
    }
}
