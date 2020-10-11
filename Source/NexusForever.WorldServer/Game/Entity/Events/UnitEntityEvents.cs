using NexusForever.WorldServer.Game.Entity.Static;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class UnitEntity : WorldEntity
    {
        /// <summary>
        /// Fires every time a regeneration tick occurs (every 0.5s)
        /// </summary>
        protected virtual void OnTickRegeneration()
        {
            // TODO: This should probably get moved to a Calculation Library/Manager at some point. There will be different timers on Stat refreshes, but right now the timer is hardcoded to every 0.25s.
            // Probably worth considering an Attribute-grouped Class that allows us to run differentt regeneration methods & calculations for each stat.

            if (Health < MaxHealth)
                Health += (uint)(MaxHealth / 200f);

            if (Shield < MaxShieldCapacity)
                Shield += (uint)(MaxShieldCapacity * GetPropertyValue(Property.ShieldRegenPct) * regenTimer.Duration);
        }
    }
}
