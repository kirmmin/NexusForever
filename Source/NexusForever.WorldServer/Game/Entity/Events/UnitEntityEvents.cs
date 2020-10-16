using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Movement;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class UnitEntity : WorldEntity
    {
        /// <summary>
        /// Fires every time a regeneration tick occurs (every 0.5s)
        /// </summary>
        protected virtual void OnTickRegeneration()
        {
            if (!IsAlive)
                return;
            // TODO: This should probably get moved to a Calculation Library/Manager at some point. There will be different timers on Stat refreshes, but right now the timer is hardcoded to every 0.25s.
            // Probably worth considering an Attribute-grouped Class that allows us to run differentt regeneration methods & calculations for each stat.

            if (Health < MaxHealth)
                Health += (uint)(MaxHealth / 200f);

            if (Shield < MaxShieldCapacity)
                Shield += (uint)(MaxShieldCapacity * GetPropertyValue(Property.ShieldRegenPct) * regenTimer.Duration);
        }
        
        public override void OnRemoveFromMap()
        {
            // TODO: Delay OnRemoveFromMap from firing immediately on DC. Allow players to die between getting disconnected and being removed from map :D
            ThreatManager.ClearThreatList();

            base.OnRemoveFromMap();
        }

        /// <summary>
        /// Invoked when <see cref="ThreatManager"/> adds a <see cref="HostileEntity"/>.
        /// </summary>
        public virtual void OnThreatAddTarget(HostileEntity hostile)
        {
            if (currentTargetUnitId == 0u)
                SetTarget(hostile.HatedUnitId, hostile.Threat);
        }

        /// <summary>
        /// Invoked when <see cref="ThreatManager"/> removes a <see cref="HostileEntity"/>.
        /// </summary>
        public virtual void OnThreatRemoveTarget(HostileEntity hostile)
        {
            SelectTarget();
        }

        /// <summary>
        /// Invoked when <see cref="ThreatManager"/> updates a <see cref="HostileEntity"/>.
        /// </summary>
        /// <param name="hostiles"></param>
        public virtual void OnThreatChange(IEnumerable<HostileEntity> hostiles)
        {
            CheckCombatStateChange(hostiles);
            SelectTarget();
        }

        /// <summary>
        /// Invoked when this <see cref="WorldEntity"/> combat state is changed.
        /// </summary>
        public virtual void OnCombatStateChange(bool inCombat)
        {
            Sheathed = !inCombat;

            switch (inCombat)
            {
                case true:
                    LeashPosition = Position;
                    LeashRotation = Rotation;
                    StandState = StandState.Stand;
                    AI?.OnEnterCombat();
                    break;
                case false:
                    StandState = StandState.State0;
                    AI?.OnExitCombat();
                    break;
            }
        }
    }
}
