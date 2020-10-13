using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NexusForever.WorldServer.Game.Combat
{
    public class ThreatManager : IUpdate, IEnumerable<HostileEntity>
    {
        private ConcurrentDictionary<uint /*unitId*/, HostileEntity> hostiles = new ConcurrentDictionary<uint, HostileEntity>();

        private UnitEntity owner;
        private UpdateTimer updateInterval = new UpdateTimer(1d);

        /// <summary>
        /// Initialise <see cref="ThreatManager"/> for a <see cref="UnitEntity"/>.
        /// </summary>
        public ThreatManager(UnitEntity owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occured.
        /// </summary>
        public void Update(double lastTick)
        {
            if (hostiles.Count == 0u || owner is Player)
                return;

            updateInterval.Update(lastTick);
            if (updateInterval.HasElapsed)
            {
                SendThreatList();
                updateInterval.Reset();
            }
        }

        /// <summary>
        /// Add threat for the provided <see cref="UnitEntity"/>.
        /// </summary>
        public void AddThreat(UnitEntity target, int threat)
        {
            // TODO: Add way to pass in the Spell that was the source
            HostileEntity hostile = GetHostile(target);
            if (hostile == null)
                throw new InvalidOperationException($"Hostile target does not exist.");

            DoAddThreat(hostile, threat);
        }

        /// <summary>
        /// Returns a <see cref="HostileEntity"/> for the given <see cref="UnitEntity"/>.
        /// </summary>
        private HostileEntity GetHostile(UnitEntity target)
        {
            if (hostiles.TryGetValue(target.Guid, out HostileEntity hostileEntity))
                return hostileEntity;

            return CreateHostile(target);
        }

        /// <summary>
        /// Instantiates a <see cref="HostileEntity"/> for the given <see cref="UnitEntity"/>.
        /// </summary>
        private HostileEntity CreateHostile(UnitEntity target)
        {
            HostileEntity hostile = new HostileEntity(owner, target);
            hostiles.TryAdd(hostile.HatedUnitId, hostile);

            owner.OnThreatAddTarget(hostile);
            hostile.GetEntity(owner).ThreatManager.AddThreat(owner, 0);

            return hostile;
        }
       
        /// <summary>
        /// Internal method to adjust threat for the given <see cref="HostileEntity"/>.
        /// </summary>
        private void DoAddThreat(HostileEntity hostile, int threat)
        {
            hostile.AdjustThreat(threat);
            owner.OnThreatChange(GetThreatList());
        }

        /// <summary>
        /// Clear the threat list and alert all entities that this <see cref="UnitEntity"/> has forgotten them.
        /// </summary>
        public void ClearThreatList()
        {
            foreach (HostileEntity hostile in hostiles.Values.ToList())
                RemoveTarget(hostile.HatedUnitId);

            hostiles.Clear();
            owner.OnThreatChange(GetThreatList());
        }

        /// <summary>
        /// Remove the target with the given unit id from this threat list, if they exist. This will alert the entity that they have been forgotten by this <see cref="UnitEntity"/>.
        /// </summary>
        public void RemoveTarget(uint unitId)
        {
            if (hostiles.TryRemove(unitId, out HostileEntity hostileEntity))
            {
                owner.OnThreatRemoveTarget(hostileEntity);

                // TODO: Handle the case of PvP where the only "end" would be death. Consider an "in-combat without threat" timer as a trigger, in PvP situations only.
                hostileEntity.GetEntity(owner)?.ThreatManager.RemoveTarget(owner.Guid);
            }

            if (hostiles.Count == 0u)
                ClearThreatList();
        }

        /// <summary>
        /// Sends <see cref="ServerThreatListUpdate"/> to all targets.
        /// </summary>
        private void SendThreatList()
        {
            var threatUpdate = new ServerThreatListUpdate
            {
                SrcUnitId = owner.Guid
            };

            HostileEntity[] hostileEntities = hostiles.Values.ToArray();
            int j = 0;
            for (uint i = 0u; i < hostileEntities.Length; i++)
            {
                // There's a hard limit of 5 entities and their threat levels in the packet. We have to create enough of the same packet to send the entire threat list.
                if (i != 0u && i % 5u == 0u)
                {
                    owner.EnqueueToVisible(threatUpdate);
                    threatUpdate = new ServerThreatListUpdate();
                    j = 0;
                }

                threatUpdate.ThreatUnitIds[j] = hostileEntities[i].HatedUnitId;
                threatUpdate.ThreatLevels[j] = hostileEntities[i].Threat;

                if (i == hostileEntities.Length - 1)
                    owner.EnqueueToVisible(threatUpdate);

                j++;
            }
        }

        /// <summary>
        /// Returns an <see cref="IEnumerable{T}}"/> containing all <see cref="HostileEntity"/> ordered in descending threat.
        /// </summary>
        public IEnumerable<HostileEntity> GetThreatList()
        {
            return hostiles.Values.OrderByDescending(i => i.Threat).AsEnumerable();
        }

        public IEnumerator<HostileEntity> GetEnumerator()
        {
            return GetThreatList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
