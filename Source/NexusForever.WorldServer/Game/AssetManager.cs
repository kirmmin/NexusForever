using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Reflection;
using NexusForever.Database.Character.Model;
using NexusForever.Database.World.Model;
using NexusForever.Shared;
using NexusForever.Shared.Database;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Quest.Static;
using NexusForever.WorldServer.Game.Static;
using NLog;

namespace NexusForever.WorldServer.Game
{
    public sealed class AssetManager : Singleton<AssetManager>
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public ImmutableDictionary<InventoryLocation, uint> InventoryLocationCapacities { get; private set; }

        /// <summary>
        /// Id to be assigned to the next created character.
        /// </summary>
        public ulong NextCharacterId => nextCharacterId++;

        /// <summary>
        /// Id to be assigned to the next created item.
        /// </summary>
        public ulong NextItemId => nextItemId++;

        /// <summary>
        /// Id to be assigned to the next created mail.
        /// </summary>
        public ulong NextMailId => nextMailId++;

        /// <summary>
        /// Id to be assigned to the next created account item.
        /// </summary>
        public ulong NextAccountItemId => nextAccountItemId++;

        private ulong nextCharacterId;
        private ulong nextItemId;
        private ulong nextMailId;
        private ulong nextAccountItemId;

        private ImmutableDictionary<uint, ImmutableList<CharacterCustomizationEntry>> characterCustomisations;
        private ImmutableList<PropertyValue> characterBaseProperties;
        private ImmutableDictionary<Class, ImmutableList<PropertyValue>> characterClassBaseProperties;

        private ImmutableDictionary<ItemSlot, ImmutableList<EquippedItem>> equippedItems;
        private ImmutableDictionary<uint, ImmutableList<ItemDisplaySourceEntryEntry>> itemDisplaySourcesEntry;
        private ImmutableDictionary<uint /*item2CategoryId*/, float /*modifier*/> itemArmorModifiers;
        private ImmutableDictionary<ItemSlot, ImmutableDictionary<Property, float>> innatePropertiesLevelScaling;
        private ImmutableDictionary<ItemSlot, ImmutableDictionary<Property, float>> innatePropertiesFlat;

        private ImmutableDictionary</*bindPointId*/ushort, Location> bindPointLocations;
        private ImmutableDictionary</*zoneId*/uint, /*tutorialId*/uint> zoneTutorials;
        private ImmutableDictionary</*creatureId*/uint, /*targetGroupIds*/ImmutableList<uint>> creatureAssociatedTargetGroups;

        private ImmutableDictionary<AccountTier, ImmutableList<RewardPropertyPremiumModifierEntry>> rewardPropertiesByTier;
        private ImmutableDictionary</*targetGroupId*/uint, /*targetGroupIds*/ImmutableList<uint>> questObjectiveTargets;
        private Dictionary<DashDirection, uint /*spell4Id*/> dashSpells = new Dictionary<DashDirection, uint>
        {
            { DashDirection.Forward, 25295 },
            { DashDirection.Backward, 25296 },
            { DashDirection.Left, 25293 },
            { DashDirection.Right, 25294 },
        };

        private AssetManager()
        {
        }

        public void Initialise()
        {
            nextCharacterId   = DatabaseManager.Instance.CharacterDatabase.GetNextCharacterId() + 1ul;
            nextItemId        = DatabaseManager.Instance.CharacterDatabase.GetNextItemId() + 1ul;
            nextMailId        = DatabaseManager.Instance.CharacterDatabase.GetNextMailId() + 1ul;
            nextAccountItemId = DatabaseManager.Instance.AuthDatabase.GetNextAccountItemId() + 1ul;

            CacheCharacterCustomisations();
            CacheCharacterBaseProperties();
            CacheCharacterClassBaseProperties();
            CacheInventoryEquipSlots();
            CacheInventoryBagCapacities();
            CacheItemDisplaySourceEntries();
            CacheItemArmorModifiers();
            CacheItemInnateProperties();
            CacheTutorials();
            CacheCreatureTargetGroups();
            CacheRewardPropertiesByTier();
            CacheBindPointPositions();
            CacheQuestObjectiveTargetGroups();
        }

        private void CacheCharacterCustomisations()
        {
            var entries = new Dictionary<uint, List<CharacterCustomizationEntry>>();
            foreach (CharacterCustomizationEntry entry in GameTableManager.Instance.CharacterCustomization.Entries)
            {
                uint primaryKey;
                if (entry.CharacterCustomizationLabelId00 == 0 && entry.CharacterCustomizationLabelId01 > 0)
                    primaryKey = (entry.Value01 << 24) | (entry.CharacterCustomizationLabelId01 << 16) | (entry.Gender << 8) | entry.RaceId;
                else
                    primaryKey = (entry.Value00 << 24) | (entry.CharacterCustomizationLabelId00 << 16) | (entry.Gender << 8) | entry.RaceId;

                if (!entries.ContainsKey(primaryKey))
                    entries.Add(primaryKey, new List<CharacterCustomizationEntry>());

                entries[primaryKey].Add(entry);
            }

            characterCustomisations = entries.ToImmutableDictionary(e => e.Key, e => e.Value.ToImmutableList());
        }

        private void CacheCharacterBaseProperties()
        {
            var entries = ImmutableList.CreateBuilder<PropertyValue>();
            foreach (PropertyBaseModel propertyModel in DatabaseManager.Instance.CharacterDatabase.GetProperties(0))
            {
                var newPropValue = new PropertyValue((Property)propertyModel.Property, propertyModel.Value, propertyModel.Value);
                entries.Add(newPropValue);
            }

            characterBaseProperties = entries.ToImmutable();
        }

        private void CacheCharacterClassBaseProperties()
        {
            ImmutableDictionary<Class, ImmutableList<PropertyValue>>.Builder entries = ImmutableDictionary.CreateBuilder<Class, ImmutableList<PropertyValue>>();
            var classList = GameTableManager.Instance.Class.Entries;

            List<PropertyBaseModel> classPropertyBases = DatabaseManager.Instance.CharacterDatabase.GetProperties(1);
            foreach (ClassEntry classEntry in classList)
            {
                Class @class = (Class)classEntry.Id;

                if (entries.ContainsKey(@class))
                    continue;

                ImmutableList<PropertyValue>.Builder propertyList = ImmutableList.CreateBuilder<PropertyValue>();
                foreach (PropertyBaseModel propertyModel in classPropertyBases)
                {
                    if (propertyModel.Subtype != (uint)@class)
                        continue;

                    var newPropValue = new PropertyValue((Property)propertyModel.Property, propertyModel.Value, propertyModel.Value);
                    propertyList.Add(newPropValue);
                }
                ImmutableList<PropertyValue> classProperties = propertyList.ToImmutable();

                entries.Add(@class, classProperties);
            }
            
            characterClassBaseProperties = entries.ToImmutable();
        }

        private void CacheInventoryEquipSlots()
        {
            var entries = ImmutableDictionary.CreateBuilder<ItemSlot, List<EquippedItem>>();
            foreach (FieldInfo field in typeof(ItemSlot).GetFields())
            {
                foreach (EquippedItemAttribute attribute in field.GetCustomAttributes<EquippedItemAttribute>())
                {
                    ItemSlot slot = (ItemSlot)field.GetValue(null);
                    if (!entries.ContainsKey(slot))
                        entries.Add(slot, new List<EquippedItem>());

                    entries[slot].Add(attribute.Slot);
                }
            }
            equippedItems = entries.ToImmutableDictionary(e => e.Key, e => e.Value.ToImmutableList());
        }

        public void CacheInventoryBagCapacities()
        {
            var entries = ImmutableDictionary.CreateBuilder<InventoryLocation, uint>();
            foreach (FieldInfo field in typeof(InventoryLocation).GetFields())
            {
                foreach (InventoryLocationAttribute attribute in field.GetCustomAttributes<InventoryLocationAttribute>())
                {
                    InventoryLocation location = (InventoryLocation)field.GetValue(null);
                    entries.Add(location, attribute.DefaultCapacity);
                }
            }

            InventoryLocationCapacities = entries.ToImmutable();
        }

        private void CacheItemDisplaySourceEntries()
        {
            var entries = new Dictionary<uint, List<ItemDisplaySourceEntryEntry>>();
            foreach (ItemDisplaySourceEntryEntry entry in GameTableManager.Instance.ItemDisplaySourceEntry.Entries)
            {
                if (!entries.ContainsKey(entry.ItemSourceId))
                    entries.Add(entry.ItemSourceId, new List<ItemDisplaySourceEntryEntry>());

                entries[entry.ItemSourceId].Add(entry);
            }

            itemDisplaySourcesEntry = entries.ToImmutableDictionary(e => e.Key, e => e.Value.ToImmutableList());
        }

        private void CacheTutorials()
        {
            var zoneEntries =  ImmutableDictionary.CreateBuilder<uint, uint>();
            foreach (TutorialModel tutorial in DatabaseManager.Instance.WorldDatabase.GetTutorialTriggers())
            {
                if (tutorial.TriggerId == 0) // Don't add Tutorials with no trigger ID
                    continue;

                if (tutorial.Type == 29 && !zoneEntries.ContainsKey(tutorial.TriggerId))
                    zoneEntries.Add(tutorial.TriggerId, tutorial.Id);
            }

            zoneTutorials = zoneEntries.ToImmutable();
        }
        
        private void CacheBindPointPositions()
        {
            var entries = ImmutableDictionary.CreateBuilder<ushort, Location>();
            foreach(BindPointEntry entry in GameTableManager.Instance.BindPoint.Entries)
            {
                ushort entryId = (ushort)entry.Id;
                Creature2Entry creatureEntity = GameTableManager.Instance.Creature2.Entries.SingleOrDefault(x => x.BindPointId == entryId);
                if (creatureEntity == null)
                    continue;

                var entityEntry = DatabaseManager.Instance.WorldDatabase.GetEntity(creatureEntity.Id);
                if (entityEntry == null)
                    continue;

                WorldEntry worldEntry = GameTableManager.Instance.World.GetEntry(entityEntry.World);
                if (worldEntry == null)
                    continue;

                Location bindPointLocation = new Location(worldEntry, new Vector3(entityEntry.X, entityEntry.Y, entityEntry.Z), new Vector3(entityEntry.Rx, entityEntry.Ry, entityEntry.Rz));

                if (!entries.ContainsKey(entryId))
                    entries.Add(entryId, bindPointLocation);
            }

            bindPointLocations = entries.ToImmutable();
        }

        private void CacheCreatureTargetGroups()
        {
            var entries = ImmutableDictionary.CreateBuilder<uint, List<uint>>();
            foreach (TargetGroupEntry entry in GameTableManager.Instance.TargetGroup.Entries)
            {
                if ((TargetGroupType)entry.Type != TargetGroupType.CreatureIdGroup)
                    continue;

                foreach (uint creatureId in entry.DataEntries)
                {
                    if (!entries.ContainsKey(creatureId))
                        entries.Add(creatureId, new List<uint>());

                    entries[creatureId].Add(entry.Id);
                }
            }

            creatureAssociatedTargetGroups = entries.ToImmutableDictionary(e => e.Key, e => e.Value.ToImmutableList());
        }
        
        private void CacheItemArmorModifiers()
        {
            var armorMods = ImmutableDictionary.CreateBuilder<uint, float>();
            foreach (Item2CategoryEntry entry in GameTableManager.Instance.Item2Category.Entries.Where(i => i.Item2FamilyId == 1))
                armorMods.Add(entry.Id, entry.ArmorModifier);

            itemArmorModifiers = armorMods.ToImmutable();
        }

        private void CacheItemInnateProperties()
        {
            ImmutableDictionary<ItemSlot, ImmutableDictionary<Property, float>>.Builder propFlat = ImmutableDictionary.CreateBuilder<ItemSlot, ImmutableDictionary<Property, float>>();
            ImmutableDictionary<ItemSlot, ImmutableDictionary<Property, float>>.Builder propScaling = ImmutableDictionary.CreateBuilder<ItemSlot, ImmutableDictionary<Property, float>>();

            foreach (var slot in DatabaseManager.Instance.CharacterDatabase.GetProperties(2).GroupBy(x => x.Subtype).Select(i => i.First()))
            {
                ImmutableDictionary<Property, float>.Builder subtypePropFlat = ImmutableDictionary.CreateBuilder<Property, float>();
                ImmutableDictionary<Property, float>.Builder subtypePropScaling = ImmutableDictionary.CreateBuilder<Property, float>();
                foreach (PropertyBaseModel propertyBase in DatabaseManager.Instance.CharacterDatabase.GetProperties(2).Where(i => i.Subtype == slot.Subtype))
                {
                    switch (propertyBase.ModType)
                    {
                        case 0:
                            subtypePropFlat.Add((Property)propertyBase.Property, propertyBase.Value);
                            break;
                        case 1:
                            subtypePropScaling.Add((Property)propertyBase.Property, propertyBase.Value);
                            break;
                    }
                }

                propFlat.Add((ItemSlot)slot.Subtype, subtypePropFlat.ToImmutable());
                propScaling.Add((ItemSlot)slot.Subtype, subtypePropScaling.ToImmutable());
            }

            innatePropertiesFlat = propFlat.ToImmutable();
            innatePropertiesLevelScaling = propScaling.ToImmutable();
        }

        private void CacheRewardPropertiesByTier()
        {
            // VIP was intended to be used in China from what I can see, you can force the VIP premium system in the client with the China game mode parameter
            // not supported as the system was unfinished
            IEnumerable<RewardPropertyPremiumModifierEntry> hybridEntries = GameTableManager.Instance
                .RewardPropertyPremiumModifier.Entries
                .Where(e => (PremiumSystem)e.PremiumSystemEnum == PremiumSystem.Hybrid)
                .ToList();

            // base reward properties are determined by current account tier and lower if fall through flag is set
            rewardPropertiesByTier = hybridEntries
                .Select(e => e.Tier)
                .Distinct()
                .ToImmutableDictionary(k => (AccountTier)k, k => hybridEntries
                    .Where(r => r.Tier == k)
                    .Concat(hybridEntries
                        .Where(r => r.Tier < k && ((RewardPropertyPremiumModiferFlags)r.Flags & RewardPropertyPremiumModiferFlags.FallThrough) != 0))
                    .ToImmutableList());
        }

        private void CacheQuestObjectiveTargetGroups()
        {
            List<TargetGroupType> unhandledTargetGroups = new List<TargetGroupType>();

            void AddToTargets(TargetGroupEntry entry, ref List<uint> targetIds)
            {
                switch ((TargetGroupType)entry.Type)
                {
                    case TargetGroupType.CreatureIdGroup:
                        targetIds.AddRange(entry.DataEntries.Where(d => d != 0u));
                        break;
                    case TargetGroupType.OtherTargetGroup:
                        foreach (uint targetGroupId in entry.DataEntries.Where(d => d != 0u))
                        {
                            TargetGroupEntry targetGroup = GameTableManager.Instance.TargetGroup.GetEntry(targetGroupId);
                            if (targetGroup == null)
                                throw new InvalidOperationException();

                            AddToTargets(targetGroup, ref targetIds);
                        }
                        break;
                    default:
                        if (!(unhandledTargetGroups.Contains((TargetGroupType)entry.Type)))
                            unhandledTargetGroups.Add((TargetGroupType)entry.Type);
                        break;
                }
            }

            var entries = ImmutableDictionary.CreateBuilder<uint, List<uint>>();
            foreach (QuestObjectiveEntry questObjectiveEntry in GameTableManager.Instance.QuestObjective.Entries
                .Where(o => o.TargetGroupIdRewardPane > 0u ||
                    (QuestObjectiveType)o.Type == QuestObjectiveType.ActivateTargetGroup ||
                    (QuestObjectiveType)o.Type == QuestObjectiveType.ActivateTargetGroupChecklist ||
                    (QuestObjectiveType)o.Type == QuestObjectiveType.KillTargetGroup ||
                    (QuestObjectiveType)o.Type == QuestObjectiveType.KillTargetGroups ||
                    (QuestObjectiveType)o.Type == QuestObjectiveType.TalkToTargetGroup))
            {
                uint targetGroupId = questObjectiveEntry.Data > 0 ? questObjectiveEntry.Data : questObjectiveEntry.TargetGroupIdRewardPane;
                if (targetGroupId == 0u)
                    continue;

                TargetGroupEntry targetGroup = GameTableManager.Instance.TargetGroup.GetEntry(targetGroupId);
                if (targetGroup == null)
                    continue;

                List<uint> targetIds = new List<uint>();
                AddToTargets(targetGroup, ref targetIds);
                entries.Add(questObjectiveEntry.Id, targetIds);
            }

            questObjectiveTargets = entries.ToImmutableDictionary(e => e.Key, e => e.Value.ToImmutableList());

            string targetGroupTypes = "";
            foreach (TargetGroupType targetGroupType in unhandledTargetGroups)
                targetGroupTypes += targetGroupType.ToString() + " ";

            log.Warn($"Unhandled TargetGroup Types for Quest Objectives: {targetGroupTypes}");
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all <see cref="CharacterCustomizationEntry"/>'s for the supplied race, sex, label and value.
        /// </summary>
        public ImmutableList<CharacterCustomizationEntry> GetPrimaryCharacterCustomisation(uint race, uint sex, uint label, uint value)
        {
            uint key = (value << 24) | (label << 16) | (sex << 8) | race;
            return characterCustomisations.TryGetValue(key, out ImmutableList<CharacterCustomizationEntry> entries) ? entries : null;
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList[T]"/> containing all base <see cref="PropertyValue"/> for any character
        /// </summary>
        public ImmutableList<PropertyValue> GetCharacterBaseProperties()
        {
            return characterBaseProperties;
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList[T]"/> containing all base <see cref="PropertyValue"/> for a character class
        /// </summary>
        public ImmutableList<PropertyValue> GetCharacterClassBaseProperties(Class @class)
        {
            return characterClassBaseProperties.TryGetValue(@class, out ImmutableList<PropertyValue> propertyValues) ? propertyValues : null;
        }
        
        /// <summary>
        /// Returns matching <see cref="CharacterCustomizationEntry"/> given input parameters
        /// </summary>
        public IEnumerable<CharacterCustomizationEntry> GetCharacterCustomisation(Dictionary<uint, uint> customisations, uint race, uint sex, uint primaryLabel, uint primaryValue)
        {
            ImmutableList<CharacterCustomizationEntry> entries = GetPrimaryCharacterCustomisation(race, sex, primaryLabel, primaryValue);
            if (entries == null)
                return Enumerable.Empty<CharacterCustomizationEntry>();

            List<CharacterCustomizationEntry> customizationEntries = new List<CharacterCustomizationEntry>();

            // Customisation has multiple results, filter with a non-zero secondary KvP.
            List<CharacterCustomizationEntry> primaryEntries = entries.Where(e => e.CharacterCustomizationLabelId01 != 0).ToList();
            if (primaryEntries.Count > 0)
            {
                // This will check all entries where there is a primary AND secondary KvP.
                foreach (CharacterCustomizationEntry customizationEntry in primaryEntries)
                {
                    // Missing primary KvP in table, skipping.
                    if (customizationEntry.CharacterCustomizationLabelId00 == 0)
                        continue;

                    // Secondary KvP not found in customisation list, skipping.
                    if (!customisations.ContainsKey(customizationEntry.CharacterCustomizationLabelId01))
                        continue;

                    // Returning match found for primary KvP and secondary KvP
                    if (customisations[customizationEntry.CharacterCustomizationLabelId01] == customizationEntry.Value01)
                        customizationEntries.Add(customizationEntry);
                }

                // Return the matching value when the primary KvP matching the table's secondary KvP
                CharacterCustomizationEntry entry = entries.FirstOrDefault(e => e.CharacterCustomizationLabelId01 == primaryLabel && e.Value01 == primaryValue);
                if (entry != null)
                    customizationEntries.Add(entry);
            }
            
            if (customizationEntries.Count == 0)
            {
                // Return the matching value when the primary KvP matches the table's primary KvP, and no secondary KvP is present.
                CharacterCustomizationEntry entry = entries.FirstOrDefault(e => e.CharacterCustomizationLabelId00 == primaryLabel && e.Value00 == primaryValue);
                if (entry != null)
                    customizationEntries.Add(entry);
                else
                {
                    entry = entries.Single(e => e.CharacterCustomizationLabelId01 == 0 && e.Value01 == 0);
                    if (entry != null)
                        customizationEntries.Add(entry);
                }
            }

            // Ensure we only return 1 entry per ItemSlot.
            return customizationEntries.GroupBy(i => i.ItemSlotId).Select(i => i.First());
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all <see cref="EquippedItem"/>'s for supplied <see cref="ItemSlot"/>.
        /// </summary>
        public ImmutableList<EquippedItem> GetEquippedBagIndexes(ItemSlot slot)
        {
            return equippedItems.TryGetValue(slot, out ImmutableList<EquippedItem> entries) ? entries : null;
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all <see cref="ItemDisplaySourceEntryEntry"/>'s for the supplied itemSource.
        /// </summary>
        public ImmutableList<ItemDisplaySourceEntryEntry> GetItemDisplaySource(uint itemSource)
        {
            return itemDisplaySourcesEntry.TryGetValue(itemSource, out ImmutableList<ItemDisplaySourceEntryEntry> entries) ? entries : null;
        }

        /// <summary>
        /// Returns a <see cref="Dictionary{TKey, TValue}"/> containing <see cref="Property"/> and associated values for given Item.
        /// </summary>
        public Dictionary<Property, float> GetInnateProperties(ItemSlot itemSlot, uint effectiveLevel, uint categoryId, float supportPowerPercentage)
        {
            Dictionary<Property, float> innateProperties = new Dictionary<Property, float>();

            var innatePropScaling = innatePropertiesLevelScaling.ContainsKey(itemSlot) ? innatePropertiesLevelScaling[itemSlot] : new Dictionary<Property, float>().ToImmutableDictionary();
            var innatePropFlat = innatePropertiesFlat.ContainsKey(itemSlot) ? innatePropertiesFlat[itemSlot] : new Dictionary<Property, float>().ToImmutableDictionary();

            // TODO: Shield reboot, max % and tick % are all the same right now. Investigate how these stats are calculated and add to method.
            foreach (KeyValuePair<Property, float> entry in innatePropFlat)
                innateProperties.TryAdd(entry.Key, entry.Value);

            foreach (KeyValuePair<Property, float> entry in innatePropScaling)
            {
                var value = entry.Value;

                if (entry.Key == Property.AssaultRating)
                {
                    if (supportPowerPercentage == 1f)
                        value = 0f;
                    else if (supportPowerPercentage == 0.5f)
                        value *= 0.3333f;
                }

                if (entry.Key == Property.SupportRating)
                {
                    if (supportPowerPercentage == -1f)
                        value = 0f;
                    else if (supportPowerPercentage == -0.5f)
                        value *= 0.3333f;
                }

                // TODO: Ensure correct values after 50 effective level. There are diminishing returns after 50 effective level to Armor.
                // At 51+ it changes to Effective Level 50 Amount + (Base Value * (EffectiveLevel - 50)).
                // i.e. Eff. Level 60 Medium Chest Armor: (50 * 25) + (8.5 * (60 - 50)) = 1335 (http://www.jabbithole.com/items/corruption-resistant-jacket-48097)
                if (entry.Key == Property.Armor)
                    if (itemArmorModifiers.TryGetValue(categoryId, out float armorMod))
                        value *= armorMod;

                if (innateProperties.ContainsKey(entry.Key))
                    innateProperties[entry.Key] = innateProperties[entry.Key] + (uint)Math.Floor(value * effectiveLevel);
                else
                    innateProperties.TryAdd(entry.Key, (uint)Math.Floor(value * effectiveLevel));
            }

            return innateProperties;
        }

        /// <summary>
        /// Returns a Tutorial ID if it's found in the Zone Tutorials cache
        /// </summary>
        public uint GetTutorialIdForZone(uint zoneId)
        {
            return zoneTutorials.TryGetValue(zoneId, out uint tutorialId) ? tutorialId : 0;
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all TargetGroup ID's associated with the creatureId.
        /// </summary>
        public ImmutableList<uint> GetTargetGroupsForCreatureId(uint creatureId)
        {
            return creatureAssociatedTargetGroups.TryGetValue(creatureId, out ImmutableList<uint> entries) ? entries : null;
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all <see cref="RewardPropertyPremiumModifierEntry"/> for the given <see cref="AccountTier"/>.
        /// </summary>
        public ImmutableList<RewardPropertyPremiumModifierEntry> GetRewardPropertiesForTier(AccountTier tier)
        {
            return rewardPropertiesByTier.TryGetValue(tier, out ImmutableList<RewardPropertyPremiumModifierEntry> entries) ? entries : ImmutableList<RewardPropertyPremiumModifierEntry>.Empty;
        }

        /// <summary>        
        /// Returns a <see cref="Location"/> for a <see cref="BindPoint"/>
        /// </summary>
        public Location GetBindPoint(ushort bindpointId)
        {
            return bindPointLocations.TryGetValue(bindpointId, out Location bindPoint) ? bindPoint : null;
        }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all target ID's associated with the questObjectiveId.
        /// </summary>
        public ImmutableList<uint> GetQuestObjectiveTargetIds(uint questObjectiveId)
        {
            return questObjectiveTargets.TryGetValue(questObjectiveId, out ImmutableList<uint> entries) ? entries : Enumerable.Empty<uint>().ToImmutableList();
        }

        /// <summary>
        /// Returns a Spell4 ID for the given <see cref="DashDirection"/>.
        /// </summary>
        public uint GetDashSpell(DashDirection direction)
        {
            return dashSpells.TryGetValue(direction, out uint spellId) ? spellId : 25295;
        }
    }
}
