using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientHousingRemodelInterior)]
    public class ClientHousingRemodelInterior : IReadable
    {
        public uint[] Unknown0 { get; private set; } = new uint[6];
        public List<DecorUpdate> Remodels { get; private set; } = new List<DecorUpdate>();

        public void Read(GamePacketReader reader)
        {
            for (int i = 0; i < Unknown0.Length; i++)
                Unknown0[i] = reader.ReadUInt();

            for (int i = 0; i < Unknown0.Length; i++)
            {
                var decor = new DecorUpdate();
                decor.Read(reader);
                Remodels.Add(decor);
            }
        }
    }
}
