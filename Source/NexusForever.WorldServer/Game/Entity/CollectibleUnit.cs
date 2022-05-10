using NexusForever.Database.World.Model;
using NexusForever.Shared.Game.Events;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.CSI;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Loot;
using NexusForever.WorldServer.Game.Quest.Static;
using NexusForever.WorldServer.Game.Spell;
using System;
using System.Linq;

namespace NexusForever.WorldServer.Game.Entity
{
    [DatabaseEntity(EntityType.CollectableUnit)]
    public class CollectableUnit : UnitEntity
    {
        private uint collectorGuid = 0u;

        public CollectableUnit()
            : base(EntityType.CollectableUnit)
        {
        }

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);
            QuestChecklistIdx = model.QuestChecklistIdx;

            if (Health == 0u)
                SetStat(Stat.Health, 101u);
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new SimpleEntityModel
            {
                CreatureId        = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }

        public override void OnInteract(Player activator)
        {

        }

        public override void OnActivateSuccess(Player activator)
        {
            Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);
            if (entry == null)
                throw new ArgumentException($"Entity {EntityId} does not have matching Creature2 entry for CreatureId {CreatureId}.");

            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.ActivateEntity, CreatureId, 1u);
            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.SucceedCSI, CreatureId, 1u);

            collectorGuid = activator.Guid;
            ModifyHealth(-Health);
        }

        protected override void OnDeathStateChange(DeathState newState)
        {
            switch (newState)
            {
                case DeathState.JustDied:
                    uint virtualItemId = 0u;
                    
                    Player player = Map.GetEntity<Player>(collectorGuid);
                    if (player == null)
                        throw new ArgumentException();

                    Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);
                    if (entry.QuestAnimStateId > 0u)
                    {
                        if (player.QuestManager.GetQuestState((ushort)entry.QuestAnimStateId) == QuestState.Accepted)
                        {
                            uint objectiveId = GameTableManager.Instance.Quest2.GetEntry(entry.QuestAnimStateId)?.Objectives[entry.QuestAnimObjectiveIndex] ?? 0u;
                            if (objectiveId == 0u)
                                throw new ArgumentException();

                            QuestObjectiveEntry objectiveEntry = GameTableManager.Instance.QuestObjective.GetEntry(objectiveId);
                            if (objectiveEntry == null)
                                throw new ArgumentException();

                             virtualItemId = objectiveEntry.Data;
                        }
                    }

                    if (virtualItemId == 0u) // Try finding active objectives that can drop it
                        foreach (Quest.Quest quest in player.QuestManager.GetActiveQuests())
                            foreach (Quest.QuestObjective objective in quest.GetActiveObjectives())
                                if (objective.IsTarget(CreatureId))
                                    virtualItemId = objective.ObjectiveInfo.Entry.Data;

                        // Reward Virtual Item
                        if (virtualItemId > 0u)
                            GlobalLootManager.Instance.GiveLoot(player.Session, GameTableManager.Instance.VirtualItem.GetEntry(virtualItemId), 1u, Guid);
                    break;
                case DeathState.Corpse:
                    Map.EnqueueRespawn(this, DateTime.UtcNow.AddSeconds(30d));
                    SetDeathState(DeathState.Dead);
                    break;
                default:
                    base.OnDeathStateChange(newState);
                    break;
            }   
        }
    }
}
