using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.Game.Events;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;
using NetworkPet = NexusForever.WorldServer.Network.Message.Model.Shared.Pet;

namespace NexusForever.WorldServer.Game.Entity
{
    public class Pet : UnitEntity
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const float FollowDistance = 3f;
        private const float FollowMinRecalculateDistance = 5f;
        private float FollowAngle = 1.5f;

        public uint OwnerGuid { get; private set; }
        public Creature2Entry Creature { get; }
        public Creature2DisplayGroupEntryEntry Creature2DisplayGroup { get; }
        public uint CastingId { get; private set; }
        public Spell4Entry Spell4Entry { get; private set; }

        private bool firstSummon = true;
        private readonly UpdateTimer followTimer = new UpdateTimer(1d);

        uint autoAttackId = 0u;
        double autoAttackTimerSeconds = 1.5d;

        public Pet(Player owner, uint creature, uint castingId, Spell4Entry spellInfo, Spell4EffectsEntry effectsEntry)
            : base(EntityType.Pet)
        {
            OwnerGuid               = owner.Guid;
            CastingId               = castingId;
            Creature                = GameTableManager.Instance.Creature2.GetEntry(creature);
            Creature2DisplayGroup   = GameTableManager.Instance.Creature2DisplayGroupEntry.Entries.SingleOrDefault(x => x.Creature2DisplayGroupId == Creature.Creature2DisplayGroupId);
            DisplayInfo             = Creature2DisplayGroup?.Creature2DisplayInfoId ?? 0u;

            if (owner.PetManager.GetCombatPetGuids().Count() > 0)
                FollowAngle *= -1f;

            Spell4Entry = spellInfo;
            Faction1    = owner.Faction1;
            Faction2    = owner.Faction2;

            UpdateStats(owner);
            ModifyHealth(MaxHealth);
            SetupAI(spellInfo, effectsEntry);
        }

        public void UpdateStats(Player owner)
        {
            var newHealth = (uint)Math.Round(owner.Health * 0.4f);
            MaxHealth = newHealth;
            Level = owner.Level;
            Sheathed = false;
            SetBaseProperty(Property.AssaultRating, owner.GetPropertyValue(Property.AssaultRating) * 0.4f);
            SetBaseProperty(Property.SupportRating, owner.GetPropertyValue(Property.SupportRating) * 0.4f);

            BuildBaseProperties();
        }

        private void SetupAI(Spell4Entry spell4Entry, Spell4EffectsEntry spell4EffectsEntry)
        {
            uint autoAttackBaseId = 0;
            switch (spell4Entry.Spell4BaseIdBaseSpell)
            {
                case 27002: // Artillery Bot
                    autoAttackBaseId = 20491;
                    break;
                case 27021: // Diminisher Bot
                    autoAttackBaseId = 20399;
                    break;
                case 26998: // Repair Bot
                    autoAttackBaseId = 32801;
                    break;
                case 27082: // Bruiser Bot
                    autoAttackBaseId = 21194;
                    break;
                default:
                    log.Warn($"Auto Attack ID unknown for Pet summoned by Base ID: {spell4Entry.Spell4BaseIdBaseSpell}");
                    autoAttackBaseId = 4208;
                    break;
            }

            autoAttackId = GlobalSpellManager.Instance.GetSpellBaseInfo(autoAttackBaseId).GetSpellInfo((byte)spell4Entry.TierIndex).Entry.Id;
            autoAttackTimerSeconds = spell4EffectsEntry.DataBits03 / 1000d;

            AI = new AI.PetAI(this, autoAttackId, autoAttackTimerSeconds);
        }

        protected override void InitialiseAI()
        {   
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new PetEntityModel
            {
                CreatureId  = Creature.Id,
                OwnerId     = OwnerGuid,
                Name        = ""
            };
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            base.OnAddToMap(map, guid, vector);

            Player owner = GetVisible<Player>(OwnerGuid);
            if (owner == null)
            {
                // this shouldn't happen, log it anyway
                log.Error($"Pet {Guid} has lost it's owner {OwnerGuid}!");
                RemoveFromMap();
                return;
            }

            if (Spell4Entry.Spell4IdPetSwitch > 0)
                owner.SpellManager.GetSpell(Spell4Entry.Spell4BaseIdBaseSpell).SetPetUnitId(Guid);
            owner.PetManager.AddPetGuid(PetType.CombatPet, guid);

            // TODO: Move ActionBars to Actionbar Manager
            if (owner.PetManager.GetCombatPetGuids().Count() == 1)
                owner.Session.EnqueueMessageEncrypted(new ServerShowActionBar
                {
                    ActionBarShortcutSetId = 299,
                    ShortcutSet = Spell.Static.ShortcutSet.PrimaryPetBar,
                    Guid = guid
                });

            // TODO: Move ActionBars to Actionbar Manager
            owner.Session.EnqueueMessageEncrypted(new ServerShowActionBar
            {
                ActionBarShortcutSetId = 499,
                ShortcutSet = owner.PetManager.GetCombatPetGuids().Count() == 0 ? Spell.Static.ShortcutSet.PetMiniBar0 : Spell.Static.ShortcutSet.PetMiniBar1,
                Guid = guid
            });

            owner.Session.EnqueueMessageEncrypted(new ServerChangePetStance
            {
                PetUnitId = guid,
                Stance = 1
            });

            followTimer.Reset();

            owner.Session.EnqueueMessageEncrypted(new ServerPlayerPet
            {
                Pet = GetPetPacket()
            });

            if (firstSummon)
            {
                //owner.Session.EnqueueMessageEncrypted(new ServerCombatLog
                //{
                //    LogType = 26,
                //    PetData = new ServerCombatLog.PetLog
                //    {
                //        CasterId = owner.Guid,
                //        TargetId = Guid,
                //        SpellId = CastingId,
                //        CombatResult = 8
                //    }
                //});

                firstSummon = false;
            }
        }

        public void SetOwnerGuid(uint guid)
        {
            OwnerGuid = guid;
        }

        public NetworkPet GetPetPacket()
        {
            return new NetworkPet
            {
                Guid = Guid,
                Stance = 1,
                ValidStances = 31,
                SummoningSpell = Spell4Entry.Id
            };
        }

        public override void OnEnqueueRemoveFromMap()
        {
            followTimer.Reset(false);
        }

        public override void OnRemoveFromMap()
        {
            Player owner = GetVisible<Player>(OwnerGuid);
            if (owner == null)
            {
                // this shouldn't happen, log it anyway
                log.Error($"Pet {Guid} has lost it's owner {OwnerGuid}!");
                base.OnRemoveFromMap();
                return;
            }

            if (!owner.CanTeleport())
            {
                base.OnRemoveFromMap();
                return;
            }

            if (DeathState != DeathState.Corpse || DeathState != DeathState.Dead)
                SetDeathState(DeathState.JustDied);

            base.OnRemoveFromMap();
        }

        private void SendPacketsOnDeath(Player owner)
        {
            // TODO: Move ActionBars to Actionbar Manager
            if (owner.PetManager.GetCombatPetGuids().Count() == 1)
                owner.Session.EnqueueMessageEncrypted(new ServerShowActionBar
                {
                    ShortcutSet = Spell.Static.ShortcutSet.PrimaryPetBar
                });

            // TODO: Move ActionBars to Actionbar Manager
            owner.Session.EnqueueMessageEncrypted(new ServerShowActionBar
            {
                ShortcutSet = owner.PetManager.GetCombatPetGuids().Count() == 1 ? Spell.Static.ShortcutSet.PetMiniBar0 : Spell.Static.ShortcutSet.PetMiniBar1
            });

            owner.Session.EnqueueMessageEncrypted(new ServerPlayerPetDespawn
            {
                Guid = Guid
            });

            owner.PetManager.RemovePetGuid(PetType.CombatPet, this);
            if (Spell4Entry.Spell4IdPetSwitch > 0)
                owner.SpellManager.GetSpell(Spell4Entry.Spell4BaseIdBaseSpell).SetPetUnitId(0u);

            if (owner.HasSpell(x => !x.IsFinished && !x.IsFinishing && x.Spell4Id == 56487, out Spell.Spell limiterDebuff))
                limiterDebuff.Finish(); // End Limiter Debuff
            if (owner.HasSpell(x => x.CastingId == CastingId, out Spell.Spell petSpell))
                petSpell.Finish(); // End Casted Spell
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            if (InCombat || !IsAlive)
                return;

            Follow(lastTick);
        }

        private void Follow(double lastTick)
        {
            followTimer.Update(lastTick);
            if (!followTimer.HasElapsed)
                return;

            Player owner = GetVisible<Player>(OwnerGuid);
            if (owner == null)
            {
                // this shouldn't happen, log it anyway
                log.Error($"VanityPet {Guid} has lost it's owner {OwnerGuid}!");
                RemoveFromMap();
                return;
            }

            // only recalculate the path to owner if distance is significant
            float distance = owner.Position.GetDistance(Position);
            if (distance < FollowMinRecalculateDistance)
                return;

            MovementManager.FollowPosition(owner, FollowDistance, FollowAngle);

            followTimer.Reset();
        }

        public Vector3 GetSpawnPosition(Player player)
        {
            float angle = -player.Rotation.X + FollowAngle;
            angle += MathF.PI / 2;

            return player.Position.GetPoint2D(angle, FollowDistance);
        }

        protected override void SelectTarget(IEnumerable<HostileEntity> hostiles = null)
        {
            base.SelectTarget(hostiles);

            hostiles ??= ThreatManager.GetThreatList();

            if (hostiles.Count() == 0)
            {
                SetTarget(0u);
                return;
            }

            if (currentTargetUnitId != hostiles.First().HatedUnitId)
                SetTarget(hostiles.First().HatedUnitId, hostiles.First().Threat);
        }

        protected override void OnDeathStateChange(DeathState newState)
        {
            switch (newState)
            {
                case DeathState.JustDied:
                    Player owner = GetVisible<Player>(OwnerGuid);
                    if (owner != null)
                        SendPacketsOnDeath(owner);
                    break;
                case DeathState.Corpse:
                    QueueEvent(new DelayEvent(TimeSpan.FromSeconds(5d), () =>
                    {
                        SetDeathState(DeathState.Dead);
                    }));
                    break;
                default:
                    break;
            }

            base.OnDeathStateChange(newState);
        }
    }
}
