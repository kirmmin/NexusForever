using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Database.World.Model;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Movement;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.PVP;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using EntityModel = NexusForever.WorldServer.Database.World.Model.Entity;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract class WorldEntity : GridEntity
    {
        public EntityType Type { get; }
        public EntityCreateFlag CreateFlags { get; set; }
        public Vector3 Rotation { get; set; } = Vector3.Zero;

        /// <summary>
        /// Property related cached data
        /// </summary>
        public Dictionary<Property, PropertyValue> Properties { get; } = new Dictionary<Property, PropertyValue>();
        private Dictionary<Property, float> BaseProperties { get; } = new Dictionary<Property, float>();
        private Dictionary<Property, Dictionary<ItemSlot, /*value*/float>> ItemProperties { get; } = new Dictionary<Property, Dictionary<ItemSlot, float>>();
        private Dictionary<Property, Dictionary</*spell4Id*/uint, PropertyModifier>> SpellProperties { get; } = new Dictionary<Property, Dictionary<uint, PropertyModifier>>();
        private HashSet<Property> DirtyProperties { get; } = new HashSet<Property>();

        public uint CreatureId { get; protected set; }
        public uint DisplayInfo { get; protected set; }
        public ushort OutfitInfo { get; protected set; }
        public Faction Faction1 { get; set; }
        public Faction Faction2 { get; set; }

        public ulong ActivePropId { get; private set; }

        public Vector3 LeashPosition { get; protected set; }
        public float LeashRange { get; protected set; } = 15f;
        public MovementManager MovementManager { get; private set; }

        public bool IsAlive => Health > 0;
        public bool InCombat { get; protected set; }

        public uint Health
        {
            get => GetStatInteger(Stat.Health) ?? 0u;
            set
            {
                SetStat(Stat.Health, Math.Clamp(value, 0u, MaxHealth)); // TODO: Confirm MaxHealth is actually the maximum health would be at.
                EnqueueToVisible(new ServerEntityHealthUpdate
                {
                    UnitId = Guid,
                    Health = Health
                });
                if (this is Player player)
                    player.Session.EnqueueMessageEncrypted(new ServerPlayerHealthUpdate
                    {
                        UnitId = Guid,
                        Health = Health,
                        Mask = (UpdateHealthMask)4
                    });
            }
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

        /// <summary>
        /// Guid of the <see cref="WorldEntity"/> currently targeted.
        /// </summary>
        public uint TargetGuid { get; set; }

        /// <summary>
        /// Guid of the <see cref="Player"/> currently controlling this <see cref="WorldEntity"/>.
        /// </summary>
        public uint ControllerGuid { get; set; }

        /// <summary>
        /// Initial stab at a timer to regenerate Health & Shield values.
        /// </summary>
        private UpdateTimer statUpdateTimer = new UpdateTimer(0.25); // TODO: Long-term this should be absorbed into individual timers for each Stat regeneration method

        protected readonly Dictionary<Stat, StatValue> stats = new Dictionary<Stat, StatValue>();

        private readonly Dictionary<ItemSlot, ItemVisual> itemVisuals = new Dictionary<ItemSlot, ItemVisual>();

        private float assaultRatingToPowerFormula = GameTableManager.Instance.GameFormula.GetEntry(1266).Datafloat0;
        private float supportRatingToPowerFormula = GameTableManager.Instance.GameFormula.GetEntry(1266).Datafloat01;

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
            CreatureId   = model.Creature;
            Rotation     = new Vector3(model.Rx, model.Ry, model.Rz);
            DisplayInfo  = model.DisplayInfo;
            OutfitInfo   = model.OutfitInfo;
            Faction1     = (Faction)model.Faction1;
            Faction2     = (Faction)model.Faction2;
            ActivePropId = model.ActivePropId;

            foreach (EntityStats statModel in model.EntityStats)
                stats.Add((Stat)statModel.Stat, new StatValue(statModel));

            BuildBaseProperties();
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            LeashPosition   = vector;
            MovementManager = new MovementManager(this, vector, Rotation);
            base.OnAddToMap(map, guid, vector);
        }

        public override void OnRemoveFromMap()
        {
            base.OnRemoveFromMap();
            MovementManager = null;
        }

        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occured.
        /// </summary>
        public override void Update(double lastTick)
        {
            MovementManager.Update(lastTick);

            statUpdateTimer.Update(lastTick);
            if (statUpdateTimer.HasElapsed)
            {
                HandleStatUpdate(lastTick);

                statUpdateTimer.Reset();
            }

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
                Commands     = MovementManager.ToList(),
                VisibleItems = itemVisuals.Values.ToList(),
                Properties   = Properties.Values.ToList(),
                Faction1     = Faction1,
                Faction2     = Faction2,
                DisplayInfo  = DisplayInfo,
                OutfitInfo   = OutfitInfo
            };

            if (ActivePropId > 0)
            {
                entityCreatePacket.WorldPlacementData = new ServerEntityCreate.WorldPlacement
                {
                    Type = 1,
                    ActivePropId = ActivePropId
                };
            }

            return entityCreatePacket;
        }

        // TODO: research the difference between a standard activation and cast activation

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> is activated.
        /// </summary>
        public virtual void OnActivate(Player activator)
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> is cast activated.
        /// </summary>
        public virtual void OnActivateCast(Player activator)
        {
            // deliberately empty
        }
        
        /// <summary>
        /// Used to build the <see cref="ServerEntityPropertiesUpdate"/> from all modified <see cref="Property"/>
        /// </summary>
        private ServerEntityPropertiesUpdate BuildPropertyUpdates()
        {
            if (!HasPendingPropertyChanges)
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

            foreach(KeyValuePair<ItemSlot, float> itemStats in GetItemProperties(property))
                value += itemStats.Value;

            foreach (PropertyModifier spellModifier in GetSpellPropertyModifiers(property).OrderBy(e => e.ModifierType))
            {
                if (spellModifier.ModifierType == ModifierType.SetBase)
                {
                    baseValue = spellModifier.Value;
                    value = baseValue;
                }

                if (spellModifier.ModifierType == ModifierType.AdjustPercent && spellModifier.Value > 0f)
                {
                    baseValue *= spellModifier.Value + 1f;
                    value = baseValue;
                }

                if (spellModifier.ModifierType == ModifierType.SetValue)
                {
                    value = spellModifier.Value;
                    break;
                }

                if (spellModifier.ModifierType == ModifierType.AdjustValue)
                    value += spellModifier.Value;

                if (spellModifier.ModifierType == ModifierType.AdjustValueStack)
                    value += spellModifier.Value * spellModifier.StackCount;
            }

            return new PropertyValue(property, baseValue, value);
        }

        /// <summary>
        /// Used on entering world to set the <see cref="WorldEntity"/> base <see cref="PropertyValue"/>
        /// </summary>
        public virtual void BuildBaseProperties()
        {
            foreach (Property property in BaseProperties.Keys)
                BuildPropertyUpdates();
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
        /// Return the base value for this <see cref="WorldEntity"/>'s <see cref="Property"/>
        /// </summary>
        private float GetBasePropertyValue(Property property)
        {
            return BaseProperties.ContainsKey(property) ? BaseProperties[property] : default;
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
            return Properties.ContainsKey(property) ? Properties[property].Value : default;
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
        
        /// Returns the calculated Assault Power for this entity
        /// </summary>
        public uint GetAssaultPower()
        {
            return (uint)Math.Round((float)GetPropertyValue(Property.AssaultRating) * assaultRatingToPowerFormula);
        }

        /// <summary>
        /// Returns the calculated Support Power for this entity
        /// </summary>
        public uint GetSupportPower()
        {
            return (uint)Math.Round((float)GetPropertyValue(Property.SupportRating) * supportRatingToPowerFormula);
        }

        /// <summary>
        /// Return the <see cref="float"/> value of the supplied <see cref="Stat"/>.
        /// </summary>
        protected float? GetStatFloat(Stat stat)
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

            if (stats.TryGetValue(stat, out StatValue statValue))
                statValue.Value = value;
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
        }

        /// <summary>
        /// Set <see cref="Stat"/> to the supplied <see cref="uint"/> value.
        /// </summary>
        protected void SetStat(Stat stat, uint value)
        {
            StatAttribute attribute = EntityManager.Instance.GetStatAttribute(stat);
            if (attribute?.Type != StatType.Integer)
                throw new ArgumentException();

            if (stats.TryGetValue(stat, out StatValue statValue))
                statValue.Value = value;
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
        }

        /// <summary>
        /// Set <see cref="Stat"/> to the supplied <see cref="Enum"/> value.
        /// </summary>
        protected void SetStat<T>(Stat stat, T value) where T : Enum, IConvertible
        {
            SetStat(stat, value.ToUInt32(null));
        }

        /// <summary>
        /// Handles regeneration of Stat Values. Used to provide a hook into the Update method, for future implementation.
        /// </summary>
        private void HandleStatUpdate(double lastTick)
        {
            if (InCombat)
                return;
                
            // TODO: This should probably get moved to a Calculation Library/Manager at some point. There will be different timers on Stat refreshes, but right now the timer is hardcoded to every 0.25s.
            // Probably worth considering an Attribute-grouped Class that allows us to run differentt regeneration methods & calculations for each stat.

            if (Health < MaxHealth)
                Health += (uint)(MaxHealth / 200f);

            if (Shield < MaxShieldCapacity)
                Shield += (uint)(MaxShieldCapacity * GetPropertyValue(Property.ShieldRegenPct) * statUpdateTimer.Duration);
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
            return itemVisuals.Values;
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

        public void DealDamage(WorldEntity victim, SpellTargetInfo.SpellTargetEffectInfo.DamageDescription damageDescription, DamageType damageType, uint damage)
        {
            if (damage < 0)
                return;

            if (victim == null || !victim.IsAlive)
                return;

            damageDescription.DamageType = damageType;
            damageDescription.CombatResult = CombatResult.Hit;
            damageDescription.RawDamage = damage;

            damage = DamageCalculator.GetBaseDamage(damage);

            // Damage reduction from armor is only calculated in PvE attacks from enemies
            if(!(this is Player) && victim is Player)
                damage = DamageCalculator.GetDamageAfterArmorMitigation(victim, damageType, damage, Level);

            bool crit;
            (crit, damage) = DamageCalculator.GetCrit(damage, GetPropertyValue(Property.BaseCritChance) + GetPropertyValue(Property.RatingCritChanceIncrease));
            if (crit)
                damageDescription.CombatResult = CombatResult.Critical;

            uint shieldedAmount = DamageCalculator.GetShieldAmount(damage, victim);
            damage -= shieldedAmount;
            damageDescription.ShieldAbsorbAmount = shieldedAmount;
            damageDescription.AdjustedDamage = damage;

            if (victim is Player playerVictim)
            {
                if (playerVictim.IsDueling && playerVictim.DuelOpponentGuid == Guid)
                {
                    if (shieldedAmount > 0)
                        playerVictim.Shield -= shieldedAmount;

                    if (damage >= playerVictim.Health)
                    {
                        playerVictim.Health = 1;
                        DuelManager.Instance.EndDuelsForPlayer(playerVictim);
                    }
                    else
                        playerVictim.Health -= damage;
                }
            }
        }
    }
}
