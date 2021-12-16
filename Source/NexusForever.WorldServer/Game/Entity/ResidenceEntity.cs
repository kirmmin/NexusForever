using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Linq;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public class ResidenceEntity : WorldEntity
    {
        public HousingPlotInfoEntry PlotEntry { get; }

        public Action<uint> OnAddToMapAction { get; }

        public ResidenceEntity(uint creatureId, HousingPlotInfoEntry housingPlotInfoEntry, Action<uint> action)
            : base(EntityType.Residence)
        {
            PlotEntry = housingPlotInfoEntry;

            Creature2DisplayGroupEntryEntry displayGroupEntry = GameTableManager.Instance.Creature2DisplayGroupEntry.Entries.FirstOrDefault(i => i.Creature2DisplayGroupId == GameTableManager.Instance.Creature2.GetEntry(creatureId).Creature2DisplayGroupId);
            if (displayGroupEntry != null)
                DisplayInfo = displayGroupEntry.Creature2DisplayInfoId;

            DisplayInfo = 21720;
            CreateFlags = EntityCreateFlag.SpawnAnimation;

            OnAddToMapAction = action;

            CreatureId = creatureId;
            WorldSocketId = (ushort)PlotEntry.WorldSocketId;

            Properties.Add(Property.BaseHealth, new PropertyValue(Property.BaseHealth, 101f, 101f));
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new ResidenceEntityModel
            {
                CreatureId = CreatureId
            };
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            Guid = guid;

            if (OnAddToMapAction != null)
                OnAddToMapAction.Invoke(guid);
            
            base.OnAddToMap(map, guid, vector);
        }
    }
}
