using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Housing.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.Shared.Game;
using NexusForever.WorldServer.Game.Spell.Static;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace NexusForever.WorldServer.Game.Housing
{
    public class Plot : ISaveCharacter, IUpdate
    {
        public ulong Id { get; }
        public byte Index { get; }

        public Plug PlugEntity { get; private set; }
        public ResidenceMapInstance Map { get; private set; }
        public List<WorldEntity> PlotEntities { get; } = new List<WorldEntity>();
        public DateTime BuildStartTime { get; private set; } = new DateTime(2018, 12, 1);

        public HousingPlotInfoEntry PlotInfoEntry
        {
            get => plotInfoEntry;
            set
            {
                plotInfoEntry = value;
                saveMask |= PlotSaveMask.PlotInfoId;
            }
        }
        private HousingPlotInfoEntry plotInfoEntry;

        public HousingPlugItemEntry PlugItemEntry
        {
            get => plugItemEntry;
            set
            {
                plugItemEntry = value;
                saveMask |= PlotSaveMask.PlugItemId;
            }
        }
        private HousingPlugItemEntry plugItemEntry;

        public HousingPlugFacing PlugFacing
        {
            get => plugFacing;
            set
            {
                plugFacing = value;
                saveMask |= PlotSaveMask.PlugFacing;
            }
        }
        private HousingPlugFacing plugFacing;

        public byte BuildState
        {
            get => buildState;
            set
            {
                buildState = value;
                saveMask |= PlotSaveMask.BuildState;
            }
        }
        private byte buildState;

        private PlotSaveMask saveMask;
        private HousingBuildEntry buildEntry = null;
        private UpdateTimer buildTimer;
        private Action buildAction;
        private SimpleCollidable yard;

        /// <summary>
        /// Create a new <see cref="Plot"/> from an existing database model.
        /// </summary>
        public Plot(ResidencePlotModel model)
        {
            Id            = model.Id;
            Index         = model.Index;
            plotInfoEntry = GameTableManager.Instance.HousingPlotInfo.GetEntry(model.PlotInfoId);
            plugItemEntry = GameTableManager.Instance.HousingPlugItem.GetEntry(model.PlugItemId);
            plugFacing    = (HousingPlugFacing)model.PlugFacing;
            buildState    = model.BuildState;
            if (buildState < 4)
                BuildState = 4;

            if (PlugItemEntry != null)
                buildEntry = GameTableManager.Instance.HousingBuild.GetEntry(PlugItemEntry.HousingBuildId);

            saveMask = PlotSaveMask.None;
        }

        /// <summary>
        /// Create a new <see cref="Plot"/> from a <see cref="HousingPlotInfoEntry"/>.
        /// </summary>
        public Plot(ulong id, HousingPlotInfoEntry entry)
        {
            Id            = id;
            Index         = (byte)entry.HousingPropertyPlotIndex;
            plotInfoEntry = entry;
            plugFacing    = HousingPlugFacing.East;

            if (entry.HousingPlugItemIdDefault != 0u)
            {
                // TODO
                // plugItemId = entry.HousingPlugItemIdDefault;
            }

            saveMask = PlotSaveMask.Create;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask == PlotSaveMask.None)
                return;

            if ((saveMask & PlotSaveMask.Create) != 0)
            {
                // plot doesn't exist in database, all infomation must be saved
                context.Add(new ResidencePlotModel
                {
                    Id         = Id,
                    Index      = Index,
                    PlotInfoId = (ushort)PlotInfoEntry.Id,
                    PlugItemId = (ushort)(PlugItemEntry?.Id ?? 0u),
                    PlugFacing = (byte)PlugFacing,
                    BuildState = BuildState
                });
            }
            else
            {
                // plot already exists in database, save only data that has been modified
                var model = new ResidencePlotModel
                {
                    Id    = Id,
                    Index = Index,
                };

                EntityEntry<ResidencePlotModel> entity = context.Attach(model);
                if ((saveMask & PlotSaveMask.PlotInfoId) != 0)
                {
                    model.PlotInfoId = (ushort)PlotInfoEntry.Id;
                    entity.Property(p => p.PlotInfoId).IsModified = true;
                }

                if ((saveMask & PlotSaveMask.PlugItemId) != 0)
                {
                    model.PlugItemId = (ushort)(PlugItemEntry?.Id ?? 0u);
                    entity.Property(p => p.PlugItemId).IsModified = true;
                }

                if ((saveMask & PlotSaveMask.PlugFacing) != 0)
                {
                    model.PlugFacing = (byte)PlugFacing;
                    entity.Property(p => p.PlugFacing).IsModified = true;
                }

                if ((saveMask & PlotSaveMask.BuildState) != 0)
                {
                    model.BuildState = BuildState;
                    entity.Property(p => p.BuildState).IsModified = true;
                }
            }

            saveMask = PlotSaveMask.None;
        }

        public void Update(double lastTick)
        {
            if (buildTimer == null)
                return;

            buildTimer.Update(lastTick);
            if (buildTimer.HasElapsed)
            {
                buildAction.Invoke();
            }
        }

        /// <summary>
        /// Used when creating plugs initially
        /// </summary>
        public void SetPlug(uint plugItemId, ResidenceMapInstance map = null)
        {
            PlugItemEntry = GameTableManager.Instance.HousingPlugItem.GetEntry(plugItemId);
            buildEntry    = GameTableManager.Instance.HousingBuild.GetEntry(PlugItemEntry.HousingBuildId);
            PlugFacing    = HousingPlugFacing.East;
            BuildState    = 4;

            if (map == null)
                return;

            Map = map;
            Map.SendResidences();
            Map.SendResidencePlots();

            CreatePlug(() =>
            {
                Map.SendResidences();
                Map.SendResidencePlots();
                EmitPlotUpdate();
            });
        }

        public void SetPlug(ResidenceMapInstance map, uint plugItemId, Player player, HousingPlugFacing plugFacing = HousingPlugFacing.East)
        {
            if (map == null || map.Entry?.Id != 1229)
            {
                SetPlug(plugItemId, map);
                return;
            }

            Map = map;
            PlugItemEntry = GameTableManager.Instance.HousingPlugItem.GetEntry(plugItemId);
            buildEntry    = GameTableManager.Instance.HousingBuild.GetEntry(PlugItemEntry.HousingBuildId);
            PlugFacing    = plugFacing;

            player.AchievementManager.CheckAchievements(player, Achievement.Static.AchievementType.HousePlugType, PlugItemEntry.HousingPlotTypeId);
            if (Index != 0u)
                player.AchievementManager.CheckAchievements(player, Achievement.Static.AchievementType.HousePlugCount, 1);

            // BuildState needs to be cleared to get rid of the plug entity properly
            BuildState = 0;
            BuildStartTime = DateTime.UtcNow;

            foreach (WorldEntity entity in PlotEntities)
                entity.Map.EnqueueRemove(entity);
            PlotEntities.Clear();

            PreBuildStart(player);

            // TODO: Move build timers to a queue system akin to Spells
            buildTimer = new UpdateTimer(buildEntry.BuildPreDelayTimeMS / 1000d);
            buildAction = PreBuildFinish;
        }

        private void PreBuildStart(Player player)
        {
            PlotPlacement plotPlacement = GlobalResidenceManager.Instance.GetPlotPlacementInformation(Index);
            yard = new SimpleCollidable(plotPlacement.YardCreatureId, plotPlacement.YardDisplayInfoId, () =>
            {
                Map.SendResidencePlots();
                player.Session.EnqueueMessageEncrypted(new ServerTutorial
                {
                    TutorialId = 142
                });

                // TODO: Figure out what this packet is for
                Map.EnqueueToAll(new Server051F
                {
                    RealmId = WorldServer.RealmId,
                    ResidenceId = Id,
                    PlotIndex = Index
                });

                player.CurrencyManager.CurrencySubtractAmount(Entity.Static.CurrencyType.Credits, 1);

                if (PlugEntity != null)
                {
                    PlugEntity.Map.EnqueueRemove(PlugEntity);
                    PlugEntity = null;
                }
                
            });
            Map.EnqueueAdd(yard, new MapPosition 
            {
                Position = plotPlacement.Position
            });
        }

        private void PreBuildFinish()
        {
            BuildState = (byte)buildEntry.ConstructionEffectsId;
            
            CreatePlug(() =>
            {
                Map.SendResidences();
                Map.SendResidencePlots();

                HandleExtrasOrScripts();

                // TODO: Move build timers to a queue system akin to Spells
                buildTimer = new UpdateTimer(1d);
                buildAction = PostBuildStart;
            });
        }

        private void PostBuildStart()
        {
            SendTutorials();
            
            // Set Plug to Built
            BuildState = 4;
            EmitPlotUpdate();

            // This Packet makes the Yard begin to collapse
            Map.EnqueueToAll(new Server088C
            {
                UnitId = yard.Guid,
                Unknown0 = true
            });

            // TODO: Move build timers to a queue system akin to Spells
            double buildTimeDelay = buildEntry.BuildPostDelayTimeMS > 0 ? buildEntry.BuildPostDelayTimeMS : 1500; // Minium 1.5s to allow Construction Yard to collapse
            buildTimer = new UpdateTimer(buildTimeDelay / 1000d);
            buildAction = PostBuildFinish;
        }

        private void PostBuildFinish()
        {
            // Remove all build actions, and the Construction Yard
            buildTimer = null;
            buildAction = null;
            yard.RemoveFromMap();
            EmitPlotUpdate();
        }

        private void CreatePlug(Action action)
        {
            PlugEntity?.RemoveFromMap(); 

            // Instatiate new plug entity and assign to Plot's PlugEntity cache
            var newPlug = new Plug(PlotInfoEntry, PlugItemEntry, action);
            Map.EnqueueAdd(newPlug, new MapPosition
            {
                Position = GlobalResidenceManager.Instance.GetPlotPlacementInformation(Index).Position
            });

            // Update plot with PlugEntity reference
            SetPlugEntity(newPlug);
        }

        public void HandleExtrasOrScripts()
        {
            switch (Index)
            {
                case 0:
                    if (PlugItemEntry.Id == 18)
                        break;

                    ResidenceEntity residenceEntity = new ResidenceEntity(6241, PlotInfoEntry, (uint guid) =>
                    {
                        // Resend the Action Bar because buttons may've been enabled after adding a house
                        var actionBarPacket = new ServerShowActionBar
                        {
                            ShortcutSet = ShortcutSet.FloatingSpellBar,
                            ActionBarShortcutSetId = 1553,
                            Guid = guid
                        };
                        Map.EnqueueToAll(actionBarPacket);
                    });
                    AddPlotEntity(residenceEntity);
                    Map.EnqueueAdd(residenceEntity, new MapPosition 
                    {
                        Position = new Vector3(1471f, -715f, 1443f)
                    });
                    break;
            }

            // TODO: Run any scripts associated with Plug, associate them into the PlotEntities List

            if (BuildState == 4)
                EmitPlotUpdate();
        }

        private void SendTutorials()
        {
            uint tutorialId = 0;

            if (Index == 0)
                tutorialId = 142;

            // TODO: If Plug Type contains Challenges, Resources, or other perks
            // tutorialId = 143;

            Map.EnqueueToAll(new ServerTutorial
            {
                TutorialId = tutorialId
            });
        }

        /// <summary>
        /// Dissociates this <see cref="Plot"/> with a <see cref="Plug"/>. Only usable if the Plot Index is not 0.
        /// </summary>
        public void RemovePlug()
        {
            if (Index == 0)
                throw new HousingException("RemovePlug should not be called on the Center Plot index");

            PlugItemEntry = null;
            PlugEntity.RemoveFromMap();
            PlugEntity = null;
            PlugFacing = HousingPlugFacing.East;
            BuildState = 0;
        }

        /// <summary>
        /// Associate this <see cref="Plot"/> with a <see cref="Plug"/>
        /// </summary>
        public void SetPlugEntity(Plug plugEntity)
        {
            PlugEntity = plugEntity;
        }

        /// <summary>
        /// 
        /// </summary>
        public float GetBuildStartTime()
        {
            float totalTime = (float)(DateTime.UtcNow.Subtract(BuildStartTime).TotalDays * -1f);

            if (BuildState == 0 || BuildState == 4)
                totalTime += -1f;

            return totalTime;
        }

        public void AddPlotEntity(WorldEntity entity)
        {
            if (entity == null)
                throw new HousingException("entity should not be null.");

            PlotEntities.Add(entity);
        }

        public IEnumerable<WorldEntity> GetPlotEntities()
        {
            return PlotEntities;
        }

        private void EmitPlotUpdate()
        {
            if (Map == null)
                return;

            Map.EnqueueToAll(new ServerHousingPlotUpdate
            {
                RealmId = WorldServer.RealmId,
                ResidenceId = Id,
                PlotIndex = Index,
                BuildStage = 0,
                BuildState = BuildState
            });
        }

        public void SetMap(ResidenceMapInstance map)
        {
            Map = map;

            if (map == null)
            {
                PlugEntity = null;
                PlotEntities.Clear();
            }
        }
    }
}
