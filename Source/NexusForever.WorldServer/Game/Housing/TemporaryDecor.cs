using NexusForever.Shared.GameTable.Model;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Housing
{
    public class TemporaryDecor : Decor
    {
        public new long DecorId;

        public TemporaryDecor(Residence residence, long decorId, HousingDecorInfoEntry entry, Vector3 position, Quaternion rotation)
        {
            Residence = residence;
            DecorId = decorId;
            Entry = entry;
            Position = position;
            Rotation = rotation;
        }
    }
}
