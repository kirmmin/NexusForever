using NexusForever.WorldServer.Game.Entity;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Combat
{
    public class HostileEntity
    {
        public UnitEntity Owner { get; private set; }
        public uint HatedUnitId { get; private set; }
        public uint Threat { get; private set; }

        /// <summary>
        /// Create a new <see cref="HostileEntity"/> for the given <see cref="UnitEntity"/>.
        /// </summary>
        public HostileEntity(UnitEntity hater, UnitEntity target)
        {
            Owner = hater;
            HatedUnitId = target.Guid;
        }

        /// <summary>
        /// Modify this <see cref="HostileEntity"/> threat by the given amount.
        /// </summary>
        /// <remarks>
        /// Value is a delta, if a negative value is supplied it will be deducted from the existing threat if any.
        /// </remarks>
        public void AdjustThreat(int threatDelta)
        {
            Threat = (uint)Math.Clamp(Threat + threatDelta, 0u, uint.MaxValue);
        }

        /// <summary>
        /// Returns the <see cref="UnitEntity"/> that this <see cref="HostileEntity"/> is associated with.
        /// </summary>
        public UnitEntity GetEntity(WorldEntity requester)
        {
            return requester?.Map?.GetEntity<UnitEntity>(HatedUnitId);
        }
    }
}
