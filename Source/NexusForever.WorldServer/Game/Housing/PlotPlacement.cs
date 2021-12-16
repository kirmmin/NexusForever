using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace NexusForever.WorldServer.Game.Housing
{
    public class PlotPlacement
    {
        public uint YardCreatureId { get; }
        public uint YardDisplayInfoId { get; }
        public Vector3 Position { get; }

        public PlotPlacement(uint creatureId, uint displayInfoId, Vector3 position)
        {
            YardCreatureId = creatureId;
            YardDisplayInfoId = displayInfoId;
            Position = position;
        }
    }
}
