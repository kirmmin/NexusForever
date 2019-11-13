using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Network.Message.Model;
using System.Linq;
using EntityModel = NexusForever.WorldServer.Database.World.Model.Entity;

namespace NexusForever.WorldServer.Game.Entity
{
    [DatabaseEntity(EntityType.NonPlayer)]
    public class NonPlayer : UnitEntity
    {
        public VendorInfo VendorInfo { get; private set; }

        public NonPlayer()
            : base(EntityType.NonPlayer)
        {
        }

        public NonPlayer(Creature2Entry entry, ulong propId, ushort plugId)
            : base(EntityType.NonPlayer)
        {
            CreatureId = entry.Id;
            DecorPropId = propId;
            DecorPlugId = plugId;
            Faction1 = (Faction)entry.FactionId;
            Faction2 = (Faction)entry.FactionId;

            Creature2DisplayGroupEntryEntry displayGroupEntry = GameTableManager.Instance.Creature2DisplayGroupEntry.Entries.FirstOrDefault(i => i.Creature2DisplayGroupId == entry.Creature2DisplayGroupId);
            if (displayGroupEntry != null)
                DisplayInfo = displayGroupEntry.Creature2DisplayInfoId;

            Creature2OutfitGroupEntryEntry outfitGroupEntry = GameTableManager.Instance.Creature2OutfitGroupEntry.Entries.FirstOrDefault(i => i.Creature2OutfitGroupId == entry.Creature2OutfitGroupId);
            if (outfitGroupEntry != null)
                OutfitInfo = (ushort)outfitGroupEntry.Creature2OutfitInfoId;

            Properties.Add(Property.BaseHealth, new PropertyValue(Property.BaseHealth, 135f, 125f));
            stats.Add(Stat.Health, new StatValue(Stat.Health, 135));
            stats.Add(Stat.Level, new StatValue(Stat.Level, 1));

            CreateFlags |= EntityCreateFlag.SpawnAnimation;
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
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new NonPlayerEntityModel
            {
                CreatureId = CreatureId,
                QuestChecklistIdx = (byte)(DecorPlugId > 0 ? 255 : 0)
            };
        }

        public override ServerEntityCreate BuildCreatePacket()
        {
            ServerEntityCreate serverEntityCreate = base.BuildCreatePacket();

            if (DecorPropId > 0)
            {
                serverEntityCreate.WorldPlacementData = new ServerEntityCreate.WorldPlacement
                {
                    Type = 1,
                    SocketId = DecorPlugId
                };
            }

            return serverEntityCreate;
        }

        private void CalculateProperties()
        {
            Creature2Entry creatureEntry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);

            // TODO: research this some more
            /*float[] values = new float[200];

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

            for (uint i = 0u; i < levelEntry.UnitPropertyValue.Length; i++)
                SetProperty((Property)i, values[i]);*/
        }
    }
}
