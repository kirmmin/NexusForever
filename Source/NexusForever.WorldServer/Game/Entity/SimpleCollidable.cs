using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public class SimpleCollidable : WorldEntity
    {
        public byte QuestChecklistIdx { get; private set; }
        private Action action;

        public SimpleCollidable(uint creatureId, uint displayInfoId, Action action = null, byte questChecklistIdx = 255)
            : base(EntityType.SimpleCollidable)
        {
            CreatureId = creatureId;
            DisplayInfo = displayInfoId;
            this.action = action;
            QuestChecklistIdx = questChecklistIdx;

            Properties.Add(Property.BaseHealth, new PropertyValue(Property.BaseHealth, 101f, 101f));
            stats.Add(Stat.Health, new StatValue(Stat.Health, 101u));
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new SimpleCollidableEntityModel
            {
                CreatureId = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            base.OnAddToMap(map, guid, vector);

            if (action != null)
                action.Invoke();
        }
    }
}
