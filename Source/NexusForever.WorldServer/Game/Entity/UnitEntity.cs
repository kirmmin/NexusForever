using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Database.World.Model;
using NexusForever.Shared.Game;
using NexusForever.Shared.Game.Events;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.AI;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Loot;
using NexusForever.WorldServer.Game.Reputation.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Game.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Script;
using NLog;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class UnitEntity : WorldEntity
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly List<Spell.Spell> pendingSpells = new();
        protected UnitAI AI { get; set; }
        public UnitAI GetAI() => AI;

        private UpdateTimer regenTimer = new UpdateTimer(0.5d);
        private UpdateTimer expireTimer = null;

        public ThreatManager ThreatManager { get; private set; }
        protected uint currentTargetUnitId;

        public bool InCombat
        {
            get => combatState == CombatState.Free ? false : true;
            private set
            {
                if (inCombat == value)
                    return;

                if (inCombat == true && value == false)
                    combatState = CombatState.Exiting;

                if (inCombat == false && value == true)
                    combatState = CombatState.Engaged;

                inCombat = value;
            }
        }
        private bool inCombat;
        private CombatState combatState;
        private bool previousCombatState;

        private Dictionary<ProcType, List<ProcInfo>> procs = new();

        public float HitRadius { get; protected set; } = 1f;

        protected UnitEntity(EntityType type)
            : base(type)
        {
            ThreatManager = new ThreatManager(this);

            InitialiseAI();
            InitialiseHitRadius();
            foreach (ProcType procType in AssetManager.HandledProcTypes)
                procs.Add(procType, new List<ProcInfo>());
        }

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);

            InitialiseHitRadius();
        }

        protected virtual void InitialiseAI()
        {
            // TODO: Allow for AI Types to be set from Database
            if (this is NonPlayer)
                AI = new UnitAI(this);
        }

        public void SetExpirationTimer(double duration)
        {
            expireTimer = new UpdateTimer(duration);
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            if (pendingSpells.Count > 0)
                for (int i = pendingSpells.Count - 1; i >= 0; i--)
                {
                    Spell.Spell spell = pendingSpells[i];
                    spell.Update(lastTick);
                    spell.LateUpdate(lastTick);
                }

            pendingSpells.RemoveAll(s => s.IsFinished);

            regenTimer.Update(lastTick);
            if (IsAlive && regenTimer.HasElapsed)
            {
                OnTickRegeneration();

                regenTimer.Reset();
            }

            ThreatManager.Update(lastTick);
            CombatStateTick();
            AI?.Update(lastTick);

            foreach (ProcInfo proc in procs.Values.SelectMany(p => p).ToList())
                proc.Update(lastTick);

            if (expireTimer != null)
            {
                expireTimer.Update(lastTick);
                if (IsAlive && expireTimer.HasElapsed)
                    ModifyHealth(-Health);
            }
        }

        /// <summary>
        /// Handle Combat State changes.
        /// </summary>
        /// <remarks>We update combat state this way to ensure that spells that check whether uses is in Combat are able to trigger effects during the same tick combat is ending.</remarks>
        private void CombatStateTick()
        {
            switch (combatState)
            {
                case CombatState.Exiting:
                    combatState = CombatState.Exited;
                    break;
                case CombatState.Exited:
                    combatState = CombatState.Free;
                    break;
            }

            if (previousCombatState != InCombat)
            {
                previousCombatState = InCombat;
                OnCombatStateChange(InCombat);

                EnqueueToVisible(new ServerUnitEnteredCombat
                {
                    UnitId = Guid,
                    InCombat = InCombat
                }, true);
            }
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
            if (!IsAlive)
                return;

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
                if (!IsAlive)
                    return;

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
                            if (HasSpell(i => i.CastingId == castingId, out Spell.Spell activeSpell))
                                activeSpell.Finish();
                        }
                    }
                }
            }

            CastMethod castMethod = (CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod;
            if (parameters.ClientSideInteraction != null)
                castMethod = CastMethod.ClientSideInteraction;

            var spell = GlobalSpellManager.Instance.NewSpell(castMethod, this, parameters);
            if (!spell.Cast())
                return;

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
        public bool HasSpell(uint spell4Id, out Spell.Spell spell, bool isCasting = false)
        {
            spell = pendingSpells.FirstOrDefault(i => i.IsCasting == isCasting && !i.IsFinished && i.Spell4Id == spell4Id);

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
        /// Check if this <see cref="UnitEntity"/> has a spell active with the provided <see cref="Func"/> predicate.
        /// </summary>
        public bool HasSpell(Func<Spell.Spell, bool> predicate, out Spell.Spell spell)
        {
            spell = pendingSpells.FirstOrDefault(predicate);

            return spell != null;
        }

        /// <summary>
        /// Finish all <see cref="Spell.Spell"/> that match the given spell4Id for this <see cref="UnitEntity"/>.
        /// </summary>
        public void FinishSpells(uint spell4Id)
        {
            foreach (Spell.Spell spell in pendingSpells.Where(i => !i.IsCasting && !i.IsFinished && i.Spell4Id == spell4Id))
                spell.Finish();
        }

        /// <summary>
        /// Finish all <see cref="Spell.Spell"/> that match the given spell4Id for this <see cref="UnitEntity"/>.
        /// </summary>
        public void FinishSpellsByGroup(uint groupId)
        {
            foreach (Spell.Spell spell in pendingSpells.Where(i => !i.IsCasting && !i.IsFinished && i.HasGroup(groupId)))
                spell.Finish();
        }

        /// <summary>
        /// Returns an active <see cref="Spell.Spell"/> that is affecting this <see cref="UnitEntity"/>
        /// </summary>
        public int GetActiveSpellCount(Func<Spell.Spell, bool> func)
        {
            // TODO: Should return a single spell if looking for ActiveSpell?

            return pendingSpells.Where(func).Count();
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
        /// Returns whether or not this <see cref="UnitEntity"/> can be attacked.
        /// </summary>
        public bool CanAttack(UnitEntity target)
        {
            if (!target.IsValidAttackTarget() || !IsValidAttackTarget())
                return false;

            if (!CanSeeEntity(target))
                return false;

            // TODO: Support PvP. For now, don't let this entity count as attackable
            if (this is Player && target is Player)
            {
                if ((((this as Player).PvPFlags & PvPFlag.Enabled) == 0) || (((target as Player).PvPFlags & PvPFlag.Enabled) == 0))
                    return false;
            }

            return target.Faction1 != (Faction)0 ? GetDispositionTo(target.Faction1) < Disposition.Friendly : false;
        }

        /// <summary>
        /// Returns whether or not this <see cref="UnitEntity"/> is an attackable target.
        /// </summary>
        public bool IsValidAttackTarget()
        {
            // TODO: Expand on this. There's bound to be flags or states that should prevent an entity from being attacked.
            return (this is Player or NonPlayer or Pet) && this is not Ghost;
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

        public override void AddVisible(GridEntity entity)
        {
            base.AddVisible(entity);

            CheckEntityRange(entity);
        }

        public override void RemoveVisible(GridEntity entity)
        {
            CheckEntityRange(entity);

            base.RemoveVisible(entity);
        }

        private void CheckEntityRange(GridEntity entity)
        {
            if (!(entity is WorldEntity we))
                return;

            if (!(this is Player))
            {
                ApplyRangeTriggers(we);
                return;
            }

            we.ApplyRangeTriggers(this);
        }

        private void InitialiseHitRadius()
        {
            if (CreatureId == 0u)
                return;
                
            Creature2Entry creatureEntry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);
            if (creatureEntry == null)
                return;

            Creature2ModelInfoEntry modelInfoEntry = GameTableManager.Instance.Creature2ModelInfo.GetEntry(creatureEntry.Creature2ModelInfoId);
            if (modelInfoEntry != null)
                HitRadius = modelInfoEntry.HitRadius * creatureEntry.ModelScale;
        }

        /// <summary>
        /// Deal damage to this <see cref="UnitEntity"/>
        /// </summary>
        public void TakeDamage(UnitEntity attacker, SpellTargetInfo.SpellTargetEffectInfo.DamageDescription damageDescription, float threatMultiplier)
        {
            if (!IsAlive || !attacker.IsAlive)
                return;

            // TODO: Calculate Threat properly
            ThreatManager.AddThreat(attacker, (int)(damageDescription.RawDamage * threatMultiplier));
            if (attacker is Player player)
                player.PetManager.ApplyThreat(this);

            Shield -= damageDescription.ShieldAbsorbAmount;
            ModifyHealth(-damageDescription.AdjustedDamage);

            if (Health == 0u && attacker != null)
                Kill(attacker);
        }

        private void Kill(UnitEntity attacker)
        {
            if (Health > 0)
                throw new InvalidOperationException("Trying to kill entity that has more than 0hp");

            if (DeathState is DeathState.JustSpawned or DeathState.Alive)
                throw new InvalidOperationException($"DeathState is incorrect! Current DeathState is {DeathState}");

            // Fire Events (OnKill, OnDeath)
            OnDeath(attacker);
        }

        private void RewardKiller(UnitEntity killer)
        {
            if (killer is Player player && this is not Player)
            {
                player.QuestManager.ObjectiveUpdate(Quest.Static.QuestObjectiveType.KillCreature, CreatureId, 1u);
                player.QuestManager.ObjectiveUpdate(Quest.Static.QuestObjectiveType.KillCreature2, CreatureId, 1u);
                player.QuestManager.ObjectiveUpdate(Quest.Static.QuestObjectiveType.KillTargetGroup, CreatureId, 1u);
                player.QuestManager.ObjectiveUpdate(Quest.Static.QuestObjectiveType.KillTargetGroups, CreatureId, 1u);
                ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnDeathRewardGrant(this, player);

                // Reward XP
                if (CreatureId > 0u)
                {
                    uint groupValue = 0u;

                    Creature2Entry creature2Entry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);
                    if (creature2Entry != null)
                        groupValue = GameTableManager.Instance.Creature2Difficulty.GetEntry(creature2Entry.Creature2DifficultyId)?.GroupValue ?? 0u;

                    player.XpManager.GrantXpForCreatureKill(Level, groupValue, GetPropertyValue(Property.XpMultiplier));
                }

                // TODO: Reward XP for PvP Kills
            }
            // Reward Loot
            // Handle Achievements
            // Schedule Respawn
        }

        protected override void OnDeathStateChange(DeathState newState)
        {
            switch (newState)
            {
                case DeathState.JustDied:
                    GenerateRewards();

                    // Clear Threat
                    ThreatManager.ClearThreatList();

                    foreach (Spell.Spell spell in pendingSpells)
                    {
                        if (spell.IsCasting)
                            spell.CancelCast(CastResult.CasterCannotBeDead);
                        else
                            spell.Finish();
                    }

                    break;
                default:
                    break;
            }

            base.OnDeathStateChange(newState);
        }

        private void GenerateRewards()
        {
            foreach (HostileEntity hostileEntity in ThreatManager.GetThreatList())
            {
                UnitEntity target = hostileEntity.GetEntity(this);
                if (target != null && target is Player player)
                {
                    // TODO: Handle rewarding PVP Kills.
                    if (this is Player)
                        continue;

                    RewardKiller(player);

                    LootInstance loot = GlobalLootManager.Instance.DropLoot(player.Session, this);
                    if (loot == null)
                        continue;

                    Loot.Add(loot);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ProcInfo ApplyProc(Spell4EffectsEntry entry)
        {
            ProcInfo proc = new ProcInfo(this, entry);

            if (procs.ContainsKey(proc.Type))
            {
                bool canApplyProc = true;
                foreach (ProcInfo procInfo in procs[proc.Type])
                {
                    if (procInfo.ApplicatorSpell4Id == proc.ApplicatorSpell4Id)
                        canApplyProc = false;
                }

                if (canApplyProc)
                    procs[proc.Type].Add(proc);
                else
                    return null;
            }
            else
            {
                log.Warn($"Unhandled ProcType {entry.DataBits00}!");
                return null;
            }
            return proc;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RemoveProc(uint spell4Id)
        {
            foreach (List<ProcInfo> procList in procs.Values.ToList())
            {
                foreach (ProcInfo proc in procList.ToList())
                {
                    if (proc.ApplicatorSpell4Id == spell4Id)
                    {
                        proc.EndTrigger();
                        procs[proc.Type].Remove(proc);
                        log.Trace($"Removed Proc {proc.Effect.Id} from {proc.Type} for Entity {Guid}.");
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void FireProc(ProcType type)
        {
            if (procs.TryGetValue(type, out List<ProcInfo> procList))
            {
                foreach (ProcInfo proc in procList)
                    proc.Trigger();
            }
        }

        public CombatState GetCombatState()
        {
            return combatState;
        }
    }
}
