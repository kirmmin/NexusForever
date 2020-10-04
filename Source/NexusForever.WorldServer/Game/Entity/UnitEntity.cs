using System;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Game.Static;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class UnitEntity : WorldEntity
    {
        private readonly List<Spell.Spell> pendingSpells = new List<Spell.Spell>();

        public ThreatManager ThreatManager { get; private set; }
        protected uint currentTargetUnitId;

        public bool InCombat
        {
            get => inCombat;
            private set
            {
                if (inCombat == value)
                    return;

                inCombat = value;
                OnCombatStateChange(value);

                EnqueueToVisible(new ServerUnitEnteredCombat
                {
                    UnitId = Guid,
                    InCombat = value
                }, true);
            }
        }
        private bool inCombat;

        protected UnitEntity(EntityType type)
            : base(type)
        {
            ThreatManager = new ThreatManager(this);
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            foreach (Spell.Spell spell in pendingSpells.ToArray())
            {
                spell.Update(lastTick);
                if (spell.IsFinished)
                    pendingSpells.Remove(spell);
            }

            ThreatManager.Update(lastTick);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied spell id and <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4Id, SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException();

            CastSpell(spell4Entry.Spell4BaseIdBaseSpell, (byte)spell4Entry.TierIndex, parameters);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied spell base id, tier and <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4BaseId, byte tier, SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            SpellBaseInfo spellBaseInfo = GlobalSpellManager.Instance.GetSpellBaseInfo(spell4BaseId);
            if (spellBaseInfo == null)
                throw new ArgumentOutOfRangeException();

            SpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
            if (spellInfo == null)
                throw new ArgumentOutOfRangeException();

            parameters.SpellInfo = spellInfo;
            CastSpell(parameters);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            if (DisableManager.Instance.IsDisabled(DisableType.BaseSpell, parameters.SpellInfo.BaseInfo.Entry.Id))
            {
                if (this is Player player)
                    player.SendSystemMessage($"Unable to cast base spell {parameters.SpellInfo.BaseInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (DisableManager.Instance.IsDisabled(DisableType.Spell, parameters.SpellInfo.Entry.Id))
            {
                if (this is Player player)
                    player.SendSystemMessage($"Unable to cast spell {parameters.SpellInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (parameters.UserInitiatedSpellCast)
            {
                if (this is Player player)
                    player.Dismount();
            }

            var spell = new Spell.Spell(this, parameters);
            spell.Cast();
            pendingSpells.Add(spell);
        }

        /// <summary>
        /// Cancel any <see cref="Spell"/>'s that are interrupted by movement.
        /// </summary>
        public void CancelSpellsOnMove()
        {
            foreach (Spell.Spell spell in pendingSpells)
                if (spell.IsMovingInterrupted() && spell.IsCasting)
                    spell.CancelCast(CastResult.CasterMovement);
        }

        /// <summary>
        /// Cancel a <see cref="Spell"/> based on its casting id
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        public void CancelSpellCast(uint castingId)
        {
            Spell.Spell spell = pendingSpells.SingleOrDefault(s => s.CastingId == castingId);
            spell?.CancelCast(CastResult.SpellCancelled);
        }

        /// <summary>
        /// Returns whether or not this <see cref="UnitEntity"/> is an attackable target.
        /// </summary>
        public bool IsValidAttackTarget()
        {
            // TODO: Expand on this. There's bound to be flags or states that should prevent an entity from being attacked.
            return (this is Player || this is NonPlayer);
        }

        private void CheckCombatStateChange(IEnumerable<HostileEntity> hostiles = null)
        {
            if (!IsValidAttackTarget())
                return;

            // TODO: Add other checks as necessary
            hostiles ??= ThreatManager.GetThreatList();

            if (hostiles.Count() > 0)
                InCombat = true;
            else
                InCombat = false;

            SelectTarget();
        }

        /// <summary>
        /// Invoked when this <see cref="WorldEntity"/> is asked to select a target for an attack.
        /// </summary>
        public virtual void SelectTarget(IEnumerable<HostileEntity> hostiles = null)
        {
            // deliberately empty
        }

        protected void SetTarget(uint targetUnitId, uint threatLevel = 0u)
        {
            if (currentTargetUnitId == targetUnitId)
                return;

            currentTargetUnitId = targetUnitId;
            EnqueueToVisible(new ServerEntityTargetUnit
            {
                UnitId = Guid,
                NewTargetId = targetUnitId,
                ThreatLevel = threatLevel
            });

            if (currentTargetUnitId != 0u)
                EnqueueToVisible(new ServerEntityAggroSwitch
                {
                    UnitId = Guid,
                    TargetId = currentTargetUnitId
                });
        }
    }
}
