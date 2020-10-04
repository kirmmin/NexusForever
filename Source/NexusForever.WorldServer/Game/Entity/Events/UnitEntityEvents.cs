using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Movement;
using NexusForever.WorldServer.Game.Map;
using System.Collections.Generic;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class UnitEntity : WorldEntity
    {
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
            if (this is Player)
                return;

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

            if (inCombat)
                StandState = Static.StandState.Stand;
            else
                StandState = Static.StandState.State0;
        }
    }
}
