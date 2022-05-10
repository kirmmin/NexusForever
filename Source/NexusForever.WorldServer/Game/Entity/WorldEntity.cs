using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Database.World.Model;
using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.CSI;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Movement;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.Map.Search;
using NexusForever.WorldServer.Game.Prerequisite;
using NexusForever.WorldServer.Game.Reputation;
using NexusForever.WorldServer.Game.Reputation.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using NexusForever.WorldServer.Script;
using NexusForever.Shared.Game.Events;
using NexusForever.WorldServer.Game.Loot;
using NexusForever.WorldServer.Game.Entity.Network.Command;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class WorldEntity : GridEntity
    {
        public EntityType Type { get; }
        public EntityCreateFlag CreateFlags { get; set; }

        public Vector3 Rotation
        {
            get => (this is Player || MovementManager == null) ? rotation : MovementManager.GetRotation();
            set
            {
                if (this is Player || MovementManager == null)
                    rotation = value; // TODO: Emit rotations nearby if Player and no client command emitted?
                
                MovementManager?.SetRotation(value);
            } 
        }
        private Vector3 rotation = Vector3.Zero;

        /// <summary>
        /// Property related cached data
        /// </summary>
        public Dictionary<Property, PropertyValue> Properties { get; } = new();
        private Dictionary<Property, float> BaseProperties { get; } = new();
        private Dictionary<Property, Dictionary<ItemSlot, /*value*/float>> ItemProperties { get; } = new();
        private Dictionary<Property, Dictionary</*spell4Id*/uint, PropertyModifier>> SpellProperties { get; } = new();
        private HashSet<Property> DirtyProperties { get; } = new();
        public Dictionary<uint, List<EntityStatus>> StatusEffects { get; } = new();

        public uint EntityId { get; protected set; }
        public uint CreatureId { get; protected set; }
        public uint DisplayInfo { get; protected set; }
        public ushort OutfitInfo { get; protected set; }
        public Faction Faction1 { get; set; }
        public Faction Faction2 { get; set; }

        public bool IsDecorEntity { get; set; }
        public byte QuestChecklistIdx { get; set; }

        public long ActivePropId { get; protected set; }
        public ushort WorldSocketId { get; protected set; }

        public Vector3 LeashPosition { get; protected set; }
        public Vector3 LeashRotation { get; protected set; }
        public float LeashRange { get; protected set; } = 50f;
        public MovementManager MovementManager { get; protected set; }

        public float RangeCheck { get; set; } = 15f;
        protected readonly Dictionary<uint, WorldEntity> inRangeEntities = new();

        private EventQueue events = new();

        public List<LootInstance> Loot { get; protected set; } = new();

        private EntitySplineModel splineInfo;

        public uint Health
        {
            get => GetStatInteger(Stat.Health) ?? 0u;
            private set => SetStat(Stat.Health, Math.Clamp(value, 0u, MaxHealth));
        }

        public uint MaxHealth
        {
            get => (uint)GetPropertyValue(Property.BaseHealth);
            set => SetBaseProperty(Property.BaseHealth, value);
        }

        public uint Shield
        {
            get => GetStatInteger(Stat.Shield) ?? 0u;
            set => SetStat(Stat.Shield, Math.Clamp(value, 0u, MaxShieldCapacity)); // TODO: Handle overshield
        }

        public uint MaxShieldCapacity
        {
            get => (uint)GetPropertyValue(Property.ShieldCapacityMax);
            set => SetBaseProperty(Property.ShieldCapacityMax, value);
        }

        [Vital(Vital.Endurance)]
        public float Endurance
        {
            get => GetStatFloat(Stat.Resource0) ?? 0u;
            set
            {
                float newVal = Math.Clamp(value, 0f, GetPropertyValue(Property.ResourceMax0));
                SetStat(Stat.Resource0, newVal);
            }
        }

        [Vital(Vital.Focus)]
        public float Focus
        {
            get => GetStatFloat(Stat.Focus) ?? 0u;
            set 
            {
                float newVal = Math.Clamp(value, 0f, GetPropertyValue(Property.BaseFocusPool));
                SetStat(Stat.Focus, newVal);
            }
        }
        
        [Vital(Vital.Dash)]
        public float Dash
        {
            get => GetStatFloat(Stat.Dash) ?? 0f;
            set
            {
                // TODO: Validate prior to setting
                float newVal = Math.Clamp(value, 0f, GetPropertyValue(Property.ResourceMax7));
                SetStat(Stat.Dash, newVal);
            }
        }

        [Vital(Vital.Resource1)]
        [Vital(Vital.KineticEnergy)]
        [Vital(Vital.Volatility)]
        [Vital(Vital.Actuator)]
        [Vital(Vital.Actuator2)]
        public float Resource1
        {
            get => GetStatFloat(Stat.Resource1) ?? 0f;
            set
            {
                // TODO: Validate prior to setting
                float newVal = Math.Clamp(value, 0f, GetPropertyValue(Property.ResourceMax1));
                SetStat(Stat.Resource1, newVal);
            }
        }

        [Vital(Vital.Resource3)]
        [Vital(Vital.SuitPower)]
        public float Resource3
        {
            get => GetStatFloat(Stat.Resource3) ?? 0f;
            set
            {
                // TODO: Validate prior to setting
                float newVal = Math.Clamp(value, 0f, GetPropertyValue(Property.ResourceMax3));
                SetStat(Stat.Resource3, newVal);
            }
        }

        [Vital(Vital.Resource4)]
        [Vital(Vital.SpellSurge)]
        public float Resource4
        {
            get => GetStatFloat(Stat.Resource4) ?? 0f;
            set
            {
                // TODO: Validate prior to setting
                float newVal = Math.Clamp(value, 0f, GetPropertyValue(Property.ResourceMax4));
                SetStat(Stat.Resource4, newVal);
            }
        }

        [Vital(Vital.InterruptArmor)]
        public float InterruptArmor
        {
            get => (float)(GetStatInteger(Stat.InterruptArmour) ?? 0f);
            set => SetStat(Stat.InterruptArmour, (uint)value);
        }

        public bool IsAlive => Health > 0u && DeathState is not DeathState.JustDied or DeathState.Corpse or DeathState.Dead;

        public uint Level
        {
            get => GetStatInteger(Stat.Level) ?? 1u;
            set
            {
                SetStat(Stat.Level, value);
                if (this is Player player)
                    player.BuildBaseProperties();
            }
        }

        public bool Sheathed
        {
            get => Convert.ToBoolean(GetStatInteger(Stat.Sheathed) ?? 0u);
            set => SetStat(Stat.Sheathed, Convert.ToUInt32(value));
        }

        public bool Stealthed => StatusEffects.Values.SelectMany(i => i).Distinct().Contains(EntityStatus.Stealth);

        public StandState StandState
        {
            get => (StandState)(GetStatInteger(Stat.StandState) ?? 0u);
            set
            {
                SetStat(Stat.StandState, (uint)value);
                EnqueueToVisible(new ServerEmote
                {
                    Guid = Guid,
                    StandState = value
                });
            }
        }

        /// <summary>
        /// Guid of the <see cref="WorldEntity"/> currently targeted.
        /// </summary>
        public uint TargetGuid { get; set; }

        /// <summary>
        /// Guid of the <see cref="Player"/> currently controlling this <see cref="WorldEntity"/>.
        /// </summary>
        public uint ControllerGuid { get; set; }

        /// <summary>
        /// This is for scripts (or database entities) to use to replace the default activation spell ID.
        /// </summary>
        public uint ActivateSpell4Id { get; set; } = 0u;

        protected readonly Dictionary<Stat, StatValue> stats = new();

        private readonly Dictionary<ItemSlot, ItemVisual> itemVisuals = new();
        private readonly Dictionary<uint /*spell4Id*/, Dictionary<ItemSlot, ItemVisual>> temporaryItemVisuals = new();

        protected DeathState DeathState 
        {
            get => deathState;
            private set
            {
                if (deathState == value)
                    return;

                deathState = value;
                OnDeathStateChange(value);
            }
        }
        private DeathState deathState = DeathState.JustSpawned;

        /// <summary>
        /// Create a new <see cref="WorldEntity"/> with supplied <see cref="EntityType"/>.
        /// </summary>
        protected WorldEntity(EntityType type)
        {
            Type = type;
        }

        /// <summary>
        /// Initialise <see cref="WorldEntity"/> from an existing database model.
        /// </summary>
        public virtual void Initialise(EntityModel model)
        {
            EntityId          = model.Id;
            CreatureId        = model.Creature;
            rotation          = new Vector3(model.Rx, model.Ry, model.Rz);
            DisplayInfo       = model.DisplayInfo;
            OutfitInfo        = model.OutfitInfo;
            Faction1          = (Faction)model.Faction1;
            Faction2          = (Faction)model.Faction2;
            QuestChecklistIdx = model.QuestChecklistIdx;
            ActivePropId      = (long)model.ActivePropId;
            WorldSocketId     = model.WorldSocketId;

            splineInfo = model.EntitySpline;

            foreach (EntityStatModel statModel in model.EntityStat)
                stats.Add((Stat)statModel.Stat, new StatValue(statModel));

            BuildBaseProperties();

            CreateFlags |= EntityCreateFlag.SpawnAnimation;
        }

        /// <summary>
        /// Initialise <see cref="WorldEntity"/> from a <see cref="Creature2Entry"/>.
        /// </summary>
        public virtual void Initialise(Creature2Entry entry)
        {
            CreatureId = entry.Id;

            BuildBaseProperties();

            CreateFlags |= EntityCreateFlag.SpawnAnimation;
        }


        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occurred.
        /// </summary>
        public override void Update(double lastTick)
        {
            MovementManager.Update(lastTick);

            events.Update(lastTick);

            var propertyUpdatePacket = BuildPropertyUpdates();
            if (propertyUpdatePacket == null)
                return;

            EnqueueToVisible(propertyUpdatePacket, true);
        }

        protected abstract IEntityModel BuildEntityModel();

        public virtual ServerEntityCreate BuildCreatePacket()
        {
            DirtyProperties.Clear();

            ServerEntityCreate entityCreatePacket =  new ServerEntityCreate
            {
                Guid         = Guid,
                Type         = Type,
                EntityModel  = BuildEntityModel(),
                CreateFlags  = (byte)CreateFlags,
                Stats        = stats.Values.ToList(),
                Commands     = MovementManager.GetInitialCommands(),
                VisibleItems = itemVisuals.Values.ToList(),
                Properties   = Properties.Values.ToList(),
                Faction1     = Faction1,
                Faction2     = Faction2,
                DisplayInfo  = DisplayInfo,
                OutfitInfo   = OutfitInfo
            };

            // Plugs should not have this portion of the packet set by this Class. The Plug Class should set it itself.
            // This is in large part due to the way Plugs are tied either to a DecorId OR Guid. Other entities do not have the same issue.
            if (!(this is Plug))
            {
                if (ActivePropId != 0u || WorldSocketId > 0)
                {
                    entityCreatePacket.WorldPlacementData = new ServerEntityCreate.WorldPlacement
                    {
                        Type = 1,
                        ActivePropId = ActivePropId,
                        SocketId = WorldSocketId
                    };
                }
            }

            return entityCreatePacket;
        }
        
        /// <summary>
        /// Used to build the <see cref="ServerEntityPropertiesUpdate"/> from all modified <see cref="Property"/>
        /// </summary>
        private ServerEntityPropertiesUpdate BuildPropertyUpdates(bool forceUpdate = false)
        {
            if (!HasPendingPropertyChanges && !forceUpdate)
                return null;
            
            ServerEntityPropertiesUpdate propertyUpdatePacket = new ServerEntityPropertiesUpdate()
            {
                UnitId = Guid
            };
            
            foreach (Property propertyUpdate in DirtyProperties)
            {
                PropertyValue propertyValue = CalculateProperty(propertyUpdate);
                if (Properties.ContainsKey(propertyUpdate))
                    Properties[propertyUpdate] = propertyValue;
                else
                    Properties.Add(propertyUpdate, propertyValue);

                OnPropertyUpdate(propertyUpdate, propertyValue.Value);

                propertyUpdatePacket.Properties.Add(propertyValue);
            }

            DirtyProperties.Clear();
            return propertyUpdatePacket;
        }

        /// <summary>
        /// Calculates and builds a <see cref="PropertyValue"/> for this Entity's <see cref="Property"/>
        /// </summary>
        private PropertyValue CalculateProperty(Property property)
        {
            float baseValue = GetBasePropertyValue(property);
            float value = baseValue;
            baseValue = GameTableManager.Instance.UnitProperty2.GetEntry((ulong)property).DefaultValue;
            float itemValue = 0f;

            foreach (KeyValuePair<ItemSlot, float> itemStats in GetItemProperties(property))
                itemValue += itemStats.Value;

            value += itemValue;

            foreach (PropertyModifier spellModifier in GetSpellPropertyModifiers(property).OrderBy(e => e.Priority))
            {
                // TODO: Investigated this a lot, and this was the best algorithm I could determine from looking at a bunch of spell effects. 
                // Should probably be checked in the client. But, the client didn't show up anything that was super obvious as the calculations came from server.
                if (spellModifier.BaseValue != 0 && spellModifier.Value != 0)
                {
                    // 1 + 0.15 = 1.15 * Amount
                    // 1 + -0.15 = 0.85 * Amount
                    // (dataBits02 + dataBits03) * Amount
                    value = (value * spellModifier.BaseValue) + spellModifier.Value;
                    
                    // TODO: Investigate how the client handles this, if it does at all. 1 _may_ be spellModifier.BaseValue, but unsure.
                    //value *= (1 + spellModifier.Value);
                    continue;
                }

                if (spellModifier.Value != 0)
                {
                    // If a decimal, it's an overall mod.
                    // dataBits03 * Amount
                    if ((spellModifier.BaseValue % 1) > 0)
                    {
                        value *= spellModifier.Value;
                        continue;
                    }

                    // Otherwise, it's an addition of existing value and this value
                    value += spellModifier.Value;
                    continue;
                }

                if (spellModifier.BaseValue != 0)
                {
                    // If a decimal, it's an overall mod.
                    // dataBits02 * Amount{
                    value *= spellModifier.BaseValue;
                    continue;
                }

                // TODO: Both values are 0 - what do we do?!
            }

            return new PropertyValue(property, baseValue, value);
        }

        /// <summary>
        /// Used on entering world to set the <see cref="WorldEntity"/> base <see cref="PropertyValue"/>
        /// </summary>
        public virtual void BuildBaseProperties()
        {
            ServerEntityPropertiesUpdate propertiesUpdate = BuildPropertyUpdates(true);

            if (!(this is Player player))
                return;

            if (!player.IsLoading)
                player.EnqueueToVisible(propertiesUpdate, true);
        }

        public bool HasPendingPropertyChanges => DirtyProperties.Count != 0;

        /// <summary>
        /// Sets the base value for a <see cref="Property"/>
        /// </summary>
        public void SetBaseProperty(Property property, float value)
        {
            if (BaseProperties.ContainsKey(property))
                BaseProperties[property] = value;
            else
                BaseProperties.Add(property, value);

            DirtyProperties.Add(property);
        }

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a Spell4Id and <see cref="PropertyModifier"/> instance
        /// </summary>
        public void AddItemProperty(Property property, ItemSlot itemSlot, float value)
        {
            if (ItemProperties.ContainsKey(property))
            {
                var itemDict = ItemProperties[property];

                if (itemDict.ContainsKey(itemSlot))
                    itemDict[itemSlot] = value;
                else
                    itemDict.Add(itemSlot, value);
            }
            else
            {
                ItemProperties.Add(property, new Dictionary<ItemSlot, float>
        {
                    { itemSlot, value }
                });
            }

            DirtyProperties.Add(property);
        }

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a Spell that is currently affecting this <see cref="WorldEntity"/>
        /// </summary>
        public void RemoveItemProperty(Property property, ItemSlot itemSlot)
        {
            if (ItemProperties.ContainsKey(property))
            {
                var itemDict = ItemProperties[property];

                if (itemDict.ContainsKey(itemSlot))
                    itemDict.Remove(itemSlot);
            }

            DirtyProperties.Add(property);
        }

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a Spell4Id and <see cref="PropertyModifier"/> instance
        /// </summary>
        public void AddSpellModifierProperty(Property property, uint spell4Id, PropertyModifier modifier)
        {
            if (SpellProperties.ContainsKey(property))
            {
                var spellDict = SpellProperties[property];

                if (spellDict.ContainsKey(spell4Id))
                    spellDict[spell4Id] = modifier;
                else
                    spellDict.Add(spell4Id, modifier);
            }
            else
            {
                SpellProperties.Add(property, new Dictionary<uint, PropertyModifier>
        {
                    { spell4Id, modifier }
                });
            }

            DirtyProperties.Add(property);
        }

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a Spell that is currently affecting this <see cref="WorldEntity"/>
        /// </summary>
        public void RemoveSpellProperty(Property property, uint spell4Id)
        {
            if (SpellProperties.ContainsKey(property))
        {
                var spellDict = SpellProperties[property];

                if (spellDict.ContainsKey(spell4Id))
                    spellDict.Remove(spell4Id);
            }

            DirtyProperties.Add(property);
        }

        /// <summary>
        /// Remove all <see cref="Property"/> modifiers by a Spell that is currently affecting this <see cref="WorldEntity"/>
        /// </summary>
        public void RemoveSpellProperties(uint spell4Id)
        {
            List<Property> propertiesWithSpell = SpellProperties.Where(i => i.Value.Keys.Contains(spell4Id)).Select(p => p.Key).ToList();

            foreach (Property property in propertiesWithSpell)
                RemoveSpellProperty(property, spell4Id);
        }

        /// <summary>
        /// Adds the visual of an <see cref="ItemDisplayEntry"/> temporarily.
        /// </summary>
        public void AddTemporaryDisplayItem(uint spell4Id, ItemDisplayEntry displayEntry)
        {
            if (displayEntry == null)
                throw new ArgumentNullException();

            Item2TypeEntry typeEntry = GameTableManager.Instance.Item2Type.GetEntry(displayEntry.Item2TypeId);
            if (typeEntry == null)
                throw new ArgumentNullException();

            ItemSlot slot = (ItemSlot)typeEntry.ItemSlotId;
            ItemVisual visual = new ItemVisual
            {
                Slot = slot,
                DisplayId = (ushort)displayEntry.Id,
                ColourSetId = (ushort)displayEntry.ItemColorSetId,
                DyeData = (int)displayEntry.DyeChannelFlags
            };

            if (temporaryItemVisuals.TryGetValue(spell4Id, out Dictionary<ItemSlot, ItemVisual> visuals))
            {
                visuals[slot] = visual;
            }
            else
                temporaryItemVisuals.Add(spell4Id, new Dictionary<ItemSlot, ItemVisual> 
                { 
                    { slot, visual }
                });

            EmitVisualUpdate();
        }

        /// <summary>
        /// Remove all temporary <see cref="ItemVisual"/> associated with a give Spell4 ID.
        /// </summary>
        /// <param name="spell4Id"></param>
        public void RemoveTemporaryDisplayItem(uint spell4Id)
        {
            if (temporaryItemVisuals.TryGetValue(spell4Id, out Dictionary<ItemSlot, ItemVisual> visuals))
            {
                temporaryItemVisuals.Remove(spell4Id);
                EmitVisualUpdate();
            }
        }

        private void EmitVisualUpdate()
        {
            var visualUpdate = new ServerEntityVisualUpdate
            {
                UnitId      = Guid,
                DisplayInfo = DisplayInfo,
                OutfitInfo  = OutfitInfo,
                ItemVisuals = GetAppearance().ToList(),
            };

            if (this is Player player)
            {
                visualUpdate.Race = (byte)player.Race;
                visualUpdate.Sex = (byte)player.Sex;
            }
            else
            {
                visualUpdate.CreatureId = CreatureId;
            }

            EnqueueToVisible(visualUpdate, true);
        }

        /// <summary>
        /// Return the base value for this <see cref="WorldEntity"/>'s <see cref="Property"/>
        /// </summary>
        private float GetBasePropertyValue(Property property)
        {
            return BaseProperties.ContainsKey(property) ? BaseProperties[property] : GameTableManager.Instance.UnitProperty2.GetEntry((ulong)property).DefaultValue;
        }

        /// <summary>
        /// Return all item property values for this <see cref="WorldEntity"/>'s <see cref="Property"/>
        /// </summary>
        private Dictionary<ItemSlot, float> GetItemProperties(Property property)
        {
            return ItemProperties.TryGetValue(property, out Dictionary<ItemSlot, float> properties) ? properties : new Dictionary<ItemSlot, float>();
        }

        /// <summary>
        /// Return all <see cref="PropertyModifier"/> for this <see cref="WorldEntity"/>'s <see cref="Property"/>
        /// </summary>
        private IEnumerable<PropertyModifier> GetSpellPropertyModifiers(Property property)
        {
            return SpellProperties.ContainsKey(property) ? SpellProperties[property].Values : Enumerable.Empty<PropertyModifier>();
        }

        /// <summary>
        /// Returns the current value for this <see cref="WorldEntity"/>'s <see cref="Property"/>
        /// </summary>
        public float GetPropertyValue(Property property)
        {
            return Properties.ContainsKey(property) ? Properties[property].Value : GameTableManager.Instance.UnitProperty2.GetEntry((ulong)property).DefaultValue;
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> has a <see cref="Property"/> updated.
        /// </summary>
        protected virtual void OnPropertyUpdate(Property property, float newValue)
        {
            switch (property)
            {
                case Property.BaseHealth:
                    if (newValue < Health)
                        Health = MaxHealth;
                    break;
                case Property.ShieldCapacityMax:
                    if (newValue < Shield)
                        Shield = MaxShieldCapacity;
                    break;
            }
        }

        /// <summary>
        /// Return the <see cref="float"/> value of the supplied <see cref="Stat"/>.
        /// </summary>
        public float? GetStatFloat(Stat stat)
        {
            StatAttribute attribute = EntityManager.Instance.GetStatAttribute(stat);
            if (attribute?.Type != StatType.Float)
                throw new ArgumentException();

            if (!stats.TryGetValue(stat, out StatValue statValue))
                return null;

            return statValue.Value;
        }

        /// <summary>
        /// Return the <see cref="uint"/> value of the supplied <see cref="Stat"/>.
        /// </summary>
        public uint? GetStatInteger(Stat stat)
        {
            StatAttribute attribute = EntityManager.Instance.GetStatAttribute(stat);
            if (attribute?.Type != StatType.Integer)
                throw new ArgumentException();

            if (!stats.TryGetValue(stat, out StatValue statValue))
                return null;

            return (uint)statValue.Value;
        }

        /// <summary>
        /// Return the <see cref="uint"/> value of the supplied <see cref="Stat"/> as an <see cref="Enum"/>.
        /// </summary>
        public T? GetStatEnum<T>(Stat stat) where T : struct, Enum
        {
            uint? value = GetStatInteger(stat);
            if (value == null)
                return null;

            return (T)Enum.ToObject(typeof(T), value.Value);
        }

        /// <summary>
        /// Set <see cref="Stat"/> to the supplied <see cref="float"/> value.
        /// </summary>
        protected void SetStat(Stat stat, float value)
        {
            StatAttribute attribute = EntityManager.Instance.GetStatAttribute(stat);
            if (attribute?.Type != StatType.Float)
                throw new ArgumentException();

            float previousValue = 0f;
            if (stats.TryGetValue(stat, out StatValue statValue))
            {
                previousValue = statValue.Value;
                statValue.Value = value;
            }
            else
            {
                statValue = new StatValue(stat, value);
                stats.Add(stat, statValue);
            }

            if (attribute.SendUpdate)
            {
                EnqueueToVisible(new ServerEntityStatUpdateFloat
                {
                    UnitId = Guid,
                    Stat   = statValue
                }, true);
            }

            OnStatChange(stat, value, previousValue);
        }

        /// <summary>
        /// Set <see cref="Stat"/> to the supplied <see cref="uint"/> value.
        /// </summary>
        protected void SetStat(Stat stat, uint value)
        {
            StatAttribute attribute = EntityManager.Instance.GetStatAttribute(stat);
            if (attribute?.Type != StatType.Integer)
                throw new ArgumentException();

            float previousValue = 0f;
            if (stats.TryGetValue(stat, out StatValue statValue))
            {
                previousValue = statValue.Value;
                statValue.Value = value;
            }
            else
            {
                statValue = new StatValue(stat, value);
                stats.Add(stat, statValue);
            }

            if (attribute.SendUpdate)
            {
                EnqueueToVisible(new ServerEntityStatUpdateInteger
                {
                    UnitId = Guid,
                    Stat   = statValue
                }, true);
            }

            OnStatChange(stat, value, previousValue);
        }

        /// <summary>
        /// Set <see cref="Stat"/> to the supplied <see cref="Enum"/> value.
        /// </summary>
        protected void SetStat<T>(Stat stat, T value) where T : Enum, IConvertible
        {
            SetStat(stat, value.ToUInt32(null));
        }

        /// <summary>
        /// Get the current value of the <see cref="Stat"/> mapped to <see cref="Vital"/>.
        /// </summary>
        public float GetVitalValue(Vital vital)
        {
            return EntityManager.Instance.GetVitalGetter(vital)?.Invoke(this) ?? 0f;
        }

        /// <summary>
        /// Set the stat value for the provided <see cref="Vital"/>.
        /// </summary>
        public void SetVital(Vital vital, float value)
        {
            var vitalHandler = EntityManager.Instance.GetVitalSetter(vital);
            if (vitalHandler == null)
                return;
                
            vitalHandler.Invoke(this, value);
        }

        /// <summary>
        /// Modify the current stat value for the <see cref="Vital"/>.
        /// </summary>
        public void ModifyVital(Vital vital, float value)
        {
            var vitalHandler = EntityManager.Instance.GetVitalSetter(vital);
            if (vitalHandler == null)
                return;

            vitalHandler.Invoke(this, GetVitalValue(vital) + value);
        }

        /// <summary>
        /// Update <see cref="ItemVisual"/> for multiple supplied <see cref="ItemSlot"/>.
        /// </summary>
        public void SetAppearance(IEnumerable<ItemVisual> visuals)
        {
            foreach (ItemVisual visual in visuals)
                SetAppearance(visual);
        }

        /// <summary>
        /// Update <see cref="ItemVisual"/> for supplied <see cref="ItemVisual"/>.
        /// </summary>
        public void SetAppearance(ItemVisual visual)
        {
            if (visual.DisplayId != 0)
            {
                if (!itemVisuals.ContainsKey(visual.Slot))
                    itemVisuals.Add(visual.Slot, visual);
                else
                    itemVisuals[visual.Slot] = visual;
            }
            else
                itemVisuals.Remove(visual.Slot);
        }

        public IEnumerable<ItemVisual> GetAppearance()
        {
            var newVisuals = new List<ItemVisual>();
            newVisuals.AddRange(itemVisuals.Values);
            newVisuals.AddRange(temporaryItemVisuals.Values.SelectMany(x => x.Values));

            return newVisuals.AsEnumerable();
        }

        /// <summary>
        /// Update the display info for the <see cref="WorldEntity"/>, this overrides any other appearance changes.
        /// </summary>
        public void SetDisplayInfo(uint displayInfo)
        {
            DisplayInfo = displayInfo;

            EnqueueToVisible(new ServerEntityVisualUpdate
            {
                UnitId      = Guid,
                DisplayInfo = DisplayInfo
            }, true);
        }

        /// <summary>
        /// Add an <see cref="EntityStatus"/> to this entity, with the provided castingId.
        /// </summary>
        public void AddStatus(uint castingId, EntityStatus status)
        {
            if (StatusEffects.TryGetValue(castingId, out List<EntityStatus> statusEffects))
                statusEffects.Add(status);
            else
                StatusEffects.Add(castingId, new List<EntityStatus>
                {
                    status
                });

            EmitStatusChange(status);
        }

        /// <summary>
        /// Remove an effect from this Entity with the given castingId
        /// </summary>
        /// <param name="castingId"></param>
        public void RemoveEffect(uint castingId)
        {
            if (StatusEffects.TryGetValue(castingId, out List<EntityStatus> statusEffects))
                foreach (EntityStatus status in statusEffects.ToArray())
                {
                    statusEffects.Remove(status);
                    EmitStatusChange(status);
                }
        }

        /// <summary>
        /// Emit an <see cref="EntityStatus"/> related update for this entity to itself and all entities in range.
        /// </summary>
        private void EmitStatusChange(EntityStatus status)
        {
            switch (status)
            {
                case EntityStatus.Stealth:
                    SendStealthUpdate();
                    break;
            }
        }

        /// <summary>
        /// Enqueue broadcast of <see cref="IWritable"/> to all visible <see cref="Player"/>'s in range.
        /// </summary>
        public void EnqueueToVisible(IWritable message, bool includeSelf = false)
        {
            // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
            foreach (WorldEntity entity in visibleEntities.Values)
            {
                if (!(entity is Player player))
                    continue;

                if (!includeSelf && (Guid == entity.Guid || ControllerGuid == entity.Guid))
                    continue;

                player.Session.EnqueueMessageEncrypted(message);
            }
        }

        /// <summary>
        /// Return <see cref="Disposition"/> between <see cref="WorldEntity"/> and <see cref="Faction"/>.
        /// </summary>
        public virtual Disposition GetDispositionTo(Faction factionId, bool primary = true)
        {
            if ((uint)factionId == 0)
                return Disposition.Unknown;

            FactionNode targetFaction = FactionManager.Instance.GetFaction(factionId);
            if (targetFaction == null)
                throw new ArgumentException($"Invalid faction {factionId}!");

            // find disposition based on faction friendships
            Disposition? dispositionFromFactionTarget = GetDispositionFromFactionFriendship(targetFaction, primary ? Faction1 : Faction2);
            if (dispositionFromFactionTarget.HasValue)
                return dispositionFromFactionTarget.Value;

            FactionNode invokeFaction = FactionManager.Instance.GetFaction(primary ? Faction1 : Faction2);
            Disposition? dispositionFromFactionInvoker = GetDispositionFromFactionFriendship(invokeFaction, factionId);
            if (dispositionFromFactionInvoker.HasValue)
                return dispositionFromFactionInvoker.Value;

            // TODO: client does a few more checks, might not be 100% accurate
            if (primary && Faction1 == factionId || !primary && Faction2 == factionId)
                return Disposition.Friendly;

            // default to neutral if we have no disposition from other sources
            return Disposition.Neutral;
        }

        private Disposition? GetDispositionFromFactionFriendship(FactionNode node, Faction factionId)
        {
            if (node == null)
                return null;

            // check if current node has required friendship
            FactionLevel? level = node.GetFriendshipFactionLevel(factionId);
            if (level.HasValue)
                return FactionNode.GetDisposition(level.Value);

            // check if parent node has required friendship
            return GetDispositionFromFactionFriendship(node.Parent, factionId);
        }

        /// <summary>
        /// Update this entity and all visible entities of this entity's Stealth state.
        /// </summary>
        private void SendStealthUpdate()
        {
            EnqueueToVisible(new ServerUnitStealth
            {
                UnitId = Guid,
                Stealthed = Stealthed
            }, true);
        }

        /// <summary>
        /// Checks if the provided <see cref="WorldEntity"/> is at a range to trigger an event on this <see cref="WorldEntity"/>.
        /// </summary>
        public virtual void ApplyRangeTriggers(WorldEntity target)
        {
            if (!HasEnteredRange(target) && IsInRange(target))
            {
                inRangeEntities.TryAdd(target.Guid, target);
                OnEnterRange(target);
            }
            else if (!IsInRange(target))
                RemoveFromRange(target);
        }

        /// <summary>
        /// Removes the provided <see cref="WorldEntity"/> from range and notifies this <see cref="WorldEntity"/>.
        /// </summary>
        public void RemoveFromRange(WorldEntity target)
        {
            if (!HasEnteredRange(target))
                return;

            inRangeEntities.Remove(target.Guid);
            OnExitRange(target);
        }

        /// <summary>
        /// Returns whether the provided <see cref="WorldEntity"/> is within this <see cref="WorldEntity"/>'s range.
        /// </summary>
        public bool IsInRange(WorldEntity target)
        {
            return Position.GetDistance(target.Position) < RangeCheck;
        }

        /// <summary>
        /// Returns whether the provided <see cref="WorldEntity"/> has entered this <see cref="WorldEntity"/>'s range.
        /// </summary>
        protected bool HasEnteredRange(WorldEntity target)
        {
            return inRangeEntities.Keys.Contains(target.Guid);
        }

        /// <summary>
        /// Modify this Entity's Health by the given value (Negative for Damage, Positive for Healing).
        /// </summary>
        public void ModifyHealth(long health)
        {
            int currentHealth = (int)Health;
            int newHealth = (int)(Health + health);

            if (newHealth <= 0)
                Health = 0;
            else if (newHealth > MaxHealth)
                Health = MaxHealth;
            else
                Health = (uint)newHealth;

            UpdateHealthMask mask = (UpdateHealthMask)4;
            if (Health <= 0 && health < 0)
                mask = (UpdateHealthMask)128;

            EnqueueToVisible(new ServerEntityHealthUpdate
            {
                UnitId = Guid,
                Health = Health
            });

            if (this is Player)
                EnqueueToVisible(new ServerPlayerHealthUpdate
                {
                    UnitId = Guid,
                    Health = Health,
                    Mask = mask
                }, true);

            if (currentHealth == 0u && newHealth > 0) // Resurrecting
            {
                SendDeathPacket(false, 2);
                SetDeathState(DeathState.JustSpawned);
            }
            else if (currentHealth > 0u && newHealth <= 0u) // Dying
            {
                SendDeathPacket(true, 7);
                SetDeathState(DeathState.JustDied);
            }
        }

        private void SendDeathPacket(bool isDead, byte deathReason)
        {
            EnqueueToVisible(new ServerEntityDeath
            {
                UnitId = Guid,
                Dead = isDead,
                Reason = deathReason,
                RezHealth = isDead ? 0u : Health
            }, true);
        }

        protected void SetDeathState(DeathState newState)
        {
            DeathState = newState;

            switch (newState)
            {
                case DeathState.JustSpawned:
                    // Do stuff on spawn
                    SetDeathState(DeathState.Alive);
                    break;
                case DeathState.Alive:
                    // Do stuff on alive
                    break;
                case DeathState.JustDied:
                    SetDeathState(DeathState.Corpse);
                    break;
                case DeathState.Corpse:
                    // Do stuff on corpse
                    break;
                case DeathState.CorpseLooted:
                    // Do stuff on corpse looted
                    break;
                case DeathState.Dead:
                    // Do stuff when entering dead state
                    if (this is Player)
                        throw new InvalidOperationException("Invalid Death State for a Player!");

                    Map.EnqueueRemove(this);
                    break;
                default:
                    break;
            }
        }

        protected virtual void OnDeathStateChange(DeathState newState)
        {
            // This will only run through method if this is not a Player and  is a Database Entity
            if (this is Player || EntityId == 0u)
                return;

            switch (newState)
            {
                case DeathState.Corpse:
                    Map.EnqueueRespawn(this, DateTime.UtcNow.AddSeconds(30d));
                    events.EnqueueEvent(new DelayEvent(TimeSpan.FromSeconds(600d), () =>
                    {
                        SetDeathState(DeathState.Dead);
                    }));

                    if (Loot.Count == 0u)
                        SetDeathState(DeathState.CorpseLooted);
                    break;
                case DeathState.CorpseLooted:
                    events.Clear();
                    events.EnqueueEvent(new DelayEvent(TimeSpan.FromSeconds(5d), () =>
                    {
                        SetDeathState(DeathState.Dead);
                    }));
                    break;
            }
        }

        /// <summary>
        /// Return the Angle in Radians between this and a Target <see cref="WorldEntity"/> from this entity's facing direction.
        /// </summary>
        /// <returns></returns>
        public float GetAngleToEntity(WorldEntity entity)
        {
            return (Position.GetAngle(entity.Position) - Rotation.X).CondenseRadianIntoRotationRadian();
        }

        public void RemoveLoot(LootInstance lootInstance)
        {
            if (lootInstance == null)
                throw new ArgumentNullException(nameof(lootInstance));

            Loot.Remove(lootInstance);

            if (Loot.Count == 0u)
                SetDeathState(DeathState.CorpseLooted);
        }

        public void QueueEvent(IEvent eventToQueue)
        {
            events.EnqueueEvent(eventToQueue);
        }

        public void InitialiseTemporaryEntity()
        {
            MovementManager = new MovementManager(this, Position, Rotation);
        }
    }
}
