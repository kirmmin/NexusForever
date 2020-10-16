using NexusForever.Database.World.Model;
using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.CSI;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Movement.Generator;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Quest.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Script;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.WorldServer.Game.AI;

namespace NexusForever.WorldServer.Game.Entity
{
    [DatabaseEntity(EntityType.NonPlayer)]
    public class NonPlayer : UnitEntity
    {
        public VendorInfo VendorInfo { get; private set; }

        private readonly UpdateTimer engageTimer = new UpdateTimer(3, false);
        private Vector3 originalRotation = Vector3.Zero;
        private uint tempTargetId = 0u;

        public NonPlayer()
            : base(EntityType.NonPlayer)
        {
        }

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);

            if (model.EntityVendor != null)
            {
                CreateFlags |= EntityCreateFlag.Vendor;
                VendorInfo = new VendorInfo(model);
            }

            CalculateProperties();

            ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnCreate(this);
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            if (engageTimer.IsTicking)
            {
                engageTimer.Update(lastTick);
                if (engageTimer.HasElapsed)
                {
                    EngageTimerElapsed();
                    engageTimer.Reset(false);
                }
            }
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new NonPlayerEntityModel
            {
                CreatureId = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }

        public override void OnActivateSuccess(Player activator)
        {
            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.ActivateEntity, CreatureId, 1u);
            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.SucceedCSI, CreatureId, 1u);

            ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnActivateSuccess(this, activator);
        }

        private void CalculateProperties()
        {
            Creature2Entry creatureEntry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);

            // TODO: research this some more
            List<Property> propertiesToBuild = new List<Property>
            {
                Property.AssaultRating,
                Property.SupportRating
            };

            float[] values = new float[200];

            CreatureLevelEntry levelEntry = GameTableManager.Instance.CreatureLevel.GetEntry(6);
            for (uint i = 0u; i < levelEntry.UnitPropertyValue.Length; i++)
                values[i] = levelEntry.UnitPropertyValue[i];

            Creature2ArcheTypeEntry archeTypeEntry = GameTableManager.Instance.Creature2ArcheType.GetEntry(creatureEntry.Creature2ArcheTypeId);
            for (uint i = 0u; i < archeTypeEntry.UnitPropertyMultiplier.Length; i++)
                values[i] *= archeTypeEntry.UnitPropertyMultiplier[i];

            Creature2DifficultyEntry difficultyEntry = GameTableManager.Instance.Creature2Difficulty.GetEntry(creatureEntry.Creature2DifficultyId);
            for (uint i = 0u; i < difficultyEntry.UnitPropertyMultiplier.Length; i++)
                values[i] *= archeTypeEntry.UnitPropertyMultiplier[i];

            Creature2TierEntry tierEntry = GameTableManager.Instance.Creature2Tier.GetEntry(creatureEntry.Creature2TierId);
            for (uint i = 0u; i < tierEntry.UnitPropertyMultiplier.Length; i++)
                values[i] *= archeTypeEntry.UnitPropertyMultiplier[i];

            foreach (Property property in propertiesToBuild)
                Properties[property] = new PropertyValue(property, values[(int)property], values[(int)property]);

            if (Health > MaxHealth)
                Properties[Property.BaseHealth] = new PropertyValue(Property.BaseHealth, Health, Health);
        }

        public override void OnEnterRange(WorldEntity entity)
        {
            base.OnEnterRange(entity);

            if (!IsAlive)
                return;

            // TODO: Remove example code below
            if (!(entity is Player))
                return;

            if (tempTargetId > 0u || InCombat)
                return;

            if (AI != null && AI.IsLeashing())
                return;

            if (GetDispositionTo(entity.Faction1) > Reputation.Static.Disposition.Hostile)
                return;

            originalRotation = Rotation;
            MovementManager.SetRotation(Position.GetRotationTo(entity.Position), true);
            tempTargetId = entity.Guid;

            CastSpell(41368, new SpellParameters
            {
                UserInitiatedSpellCast = false
            });

            engageTimer.Resume();
        }

        public override void OnExitRange(WorldEntity entity)
        {
            base.OnExitRange(entity);

            if (!IsAlive)
                return;

            // TODO: Remove example code below
            if (!(entity is Player))
                return;

            if (tempTargetId > 0 && tempTargetId != entity.Guid)
                return;

            if (InCombat)
                return;

            if (AI != null && AI.IsLeashing())
                return;
            
            if (tempTargetId == entity.Guid)
            {
                MovementManager.SetRotation(originalRotation, blend: true);
                tempTargetId = 0u;
                engageTimer.Reset(false);
            }
        }

        private void EngageTimerElapsed()
        {
            UnitEntity target = GetVisible<UnitEntity>(tempTargetId);
            if (tempTargetId == 0u || target == null)
            {
                AI.ExitCombat();
                return;
            }

            tempTargetId = 0u;
            ThreatManager.AddThreat(target, 1);
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
    }
}
