using NexusForever.Database.World.Model;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.CSI;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Quest.Static;
using NexusForever.WorldServer.Game.Spell;

namespace NexusForever.WorldServer.Game.Entity
{
    [DatabaseEntity(EntityType.CollectableUnit)]
    public class CollectableUnit : UnitEntity
    {
        public byte QuestChecklistIdx { get; private set; }

        public CollectableUnit()
            : base(EntityType.CollectableUnit)
        {
        }

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);
            QuestChecklistIdx = model.QuestChecklistIdx;
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new SimpleEntityModel
            {
                CreatureId        = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }

        public override void OnActivate(Player activator)
        {

        }

        public override void OnActivateCast(Player activator, uint interactionId)
        {
            Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);

            // TODO: Handle casting activate spells at correct times. Additionally, ensure Prerequisites are met to cast.
            uint spell4Id = 116;
            if (entry.Spell4IdActivate.Length > 0)
            {
                for (int i = entry.Spell4IdActivate.Length - 1; i > -1; i--)
                {
                    if (entry.Spell4IdActivate[i] == 0)
                        continue;

                    spell4Id = entry.Spell4IdActivate[i];
                    break;
                }
            }

            SpellParameters parameters = new SpellParameters
            {
                PrimaryTargetId = Guid,
                ClientSideInteraction = new ClientSideInteraction(activator, this, interactionId),
                CastTimeOverride = entry.ActivateSpellCastTime,
            };
            activator.CastSpell(spell4Id, parameters);
        }

        public override void OnActivateSuccess(Player activator)
        {
            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.ActivateEntity, CreatureId, 1u);
            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.SucceedCSI, CreatureId, 1u);
        }
    }
}
