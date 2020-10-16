using System;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Shared.Game;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.AI;
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
        private readonly List<Spell.Spell> pendingSpells = new();
        protected UnitAI AI { get; set; }
        public UnitAI GetAI() => AI;

        private UpdateTimer regenTimer = new UpdateTimer(0.5d);

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

            InitialiseAI();
        }

        private void InitialiseAI()
        {
            // TODO: Allow for AI Types to be set from Database
            if (this is NonPlayer)
                AI = new UnitAI(this);
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

            regenTimer.Update(lastTick);
            if (regenTimer.HasElapsed)
            {
                OnTickRegeneration();

                regenTimer.Reset();
            }

            ThreatManager.Update(lastTick);
            AI?.Update(lastTick);
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
                throw new ArgumentOutOfRangeException("spell4Id", $"{spell4Id} not found in Spell4 Entries.");

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

            if (parameters.ClientSideInteraction != null)
                parameters.ClientSideInteraction.SetClientSideInteractionEntry(GameTableManager.Instance.ClientSideInteraction.GetEntry(spellBaseInfo.Entry.ClientSideInteractionId));

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

            // Cancel certain Spells / Buffs if required, when another ability is cast.
            // TODO: Improve this with certain rules, as there will be abilities that can be cast while stealthed, etc.
            if (parameters.UserInitiatedSpellCast)
            {
                if (this is Player player)
                    player.Dismount();
                    
                // TODO: This "effect" of removing Stealth when abilities are cast is handled by a Proc effect in the original spell. It'll trigger the removal of this buff when a player uses an ability. Once Procs are implemented, this can be removed.
                uint[] ignoredStealthBaseIds = new uint[]
                {
                    30075,
                    23164,
                    30076
                };
                if (Stealthed && !ignoredStealthBaseIds.Contains(parameters.SpellInfo.Entry.Spell4BaseIdBaseSpell))
                {
                    foreach ((uint castingId, List<EntityStatus> statuses) in StatusEffects)
                    {
                        if (statuses.Contains(EntityStatus.Stealth))
                        {
                            Spell.Spell activeSpell = GetActiveSpell(i => i.CastingId == castingId);
                            activeSpell.Finish();
                        }
                    }
                }
            }                        

            var spell = new Spell.Spell(this, parameters);
            spell.Cast();

            // Don't store spell if it failed to initialise
            if (spell.IsFailed)
                return;

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
        /// Checks if this <see cref="UnitEntity"/> is currently casting a spell.
        /// </summary>
        /// <returns></returns>
        public bool IsCasting()
        {
            foreach (Spell.Spell spell in pendingSpells)
                if (spell.IsCasting)
                    return true;

            return false;
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
        /// Check if this <see cref="UnitEntity"/> has a spell active with the provided <see cref="Spell4Entry"/> Id
        /// </summary>
        public bool HasSpell(uint spell4Id, out Spell.Spell spell)
        {
            spell = pendingSpells.FirstOrDefault(i => !i.IsCasting && !i.IsFinished && i.Spell4Id == spell4Id);

            return spell != null;
        }

        /// <summary>
        /// Check if this <see cref="UnitEntity"/> has a spell active with the provided <see cref="CastMethod"/>
        /// </summary>
        public bool HasSpell(CastMethod castMethod, out Spell.Spell spell)
        {
            spell = pendingSpells.FirstOrDefault(i => !i.IsCasting && !i.IsFinished && i.CastMethod == castMethod);

            return spell != null;
        }

        /// <summary>
        /// Returns an active <see cref="Spell.Spell"/> that is affecting this <see cref="UnitEntity"/>
        /// </summary>
        public Spell.Spell GetActiveSpell(Func<Spell.Spell, bool> func)
        {
            // TODO: Should return a single spell if looking for ActiveSpell?

            return pendingSpells.FirstOrDefault(func);
        }

        /// <summary>
        /// Returns a pending <see cref="Spell.Spell"/> based on its casting id
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to return</param>
        public Spell.Spell GetPendingSpell(uint castingId)
        {
            Spell.Spell spell = pendingSpells.SingleOrDefault(s => s.CastingId == castingId);
            return spell ?? null;
        }

        /// <summary>
        /// Returns target <see cref="UnitEntity"/> if it exists.
        /// </summary>
        public bool GetCurrentVictim(out UnitEntity unitEntity)
        {
            unitEntity = GetVisible<UnitEntity>(currentTargetUnitId);
            return unitEntity != null;
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

        protected virtual void SelectTarget(IEnumerable<HostileEntity> hostiles = null)
        {
            // Deliberately empty
        }

        protected void SetTarget(uint targetUnitId, uint threatLevel = 0u)
        {
            if (this is Player || currentTargetUnitId == targetUnitId)
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
