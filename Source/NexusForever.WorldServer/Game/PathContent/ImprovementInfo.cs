using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace NexusForever.WorldServer.Game.PathContent
{
    public class ImprovementInfo
    {
        public Vector3 Position { get; private set; }
        public uint CreatureId { get; private set; }
        public uint DisplayInfo { get; private set; }

        public ImprovementInfo(Vector3 position, uint creatureId, uint displayInfo)
        {
            Position = position;
            CreatureId = creatureId;
            DisplayInfo = displayInfo;
        }

    }
}
