using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Shared.Game.Map;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.Network;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Housing;
using NexusForever.WorldServer.Game.Housing.Static;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;

namespace NexusForever.WorldServer.Game.Map
{
    public class ResidenceMap : BaseMap
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public ulong Id => residence?.Id ?? 0ul;
        // housing maps have unlimited vision range.
        public override float VisionRange { get; protected set; } = -1f;

        private Residence residence;

        public override void Initialise(MapInfo info, Player player)
        {
            base.Initialise(info, player);

            IsStatic = true;

            if (info.ResidenceId != 0u)
            {
                residence = ResidenceManager.Instance.GetCachedResidence(info.ResidenceId);
                if (residence == null)
                    throw new InvalidOperationException();
            }
            else
                residence = ResidenceManager.Instance.CreateResidence(player);

            // initialise plug entities
            foreach (Plot plot in residence.GetPlots().Where(p => p.PlugEntry != null))
            {
                var plug = new Plug(plot.PlotEntry, plot.PlugEntry);
                EnqueueAdd(plug, Vector3.Zero);
                plot.SetPlugEntity(plug);
            }
        }

        public override void OnAddToMap(Player player)
        {
            if (residence == null)
                throw new InvalidOperationException();

            SendHousingPrivacy(player);
            SendHousingProperties(player);
            SendHousingPlots(player);

            // this shows the housing toolbar, might need to move this to a more generic place in the future
            player.Session.EnqueueMessageEncrypted(new ServerShowActionBar
            {
                ShortcutSet = ShortcutSet.FloatingSpellBar,
                ActionBarShortcutSetId = 1553,
                Guid = player.Guid
            });

            SendResidenceDecor(player);
        }

        private void SendHousingPrivacy(Player player = null)
        {
            var housingPrivacy = new ServerHousingPrivacy
            {
                ResidenceId = residence.Id,
                NeighbourhoodId = 0x190000000000000A, // magic numbers are bad
                PrivacyLevel = ResidencePrivacyLevel.Public
            };

            if (player != null)
                player.Session.EnqueueMessageEncrypted(housingPrivacy);
            else
                EnqueueToAll(housingPrivacy);
        }

        private void SendHousingProperties(Player player = null)
        {
            var housingProperties = new ServerHousingProperties
            {
                Residences =
                {
                    new ServerHousingProperties.Residence
                    {
                        RealmId           = WorldServer.RealmId,
                        ResidenceId       = residence.Id,
                        NeighbourhoodId   = 0x190000000000000A,
                        CharacterIdOwner  = residence.OwnerId,
                        Name              = residence.Name,
                        PropertyInfoId    = residence.PropertyInfoId,
                        ResidenceInfoId   = residence.ResidenceInfoEntry?.Id ?? 0u,
                        WallpaperExterior = residence.Wallpaper,
                        Entryway          = residence.Entryway,
                        Roof              = residence.Roof,
                        Door              = residence.Door,
                        Ground            = residence.Ground,
                        Music             = residence.Music,
                        Sky               = residence.Sky,
                        Flags             = residence.Flags,
                        ResourceSharing   = residence.ResourceSharing,
                        GardenSharing     = residence.GardenSharing
                    }
                }
            };

            if (player != null)
                player.Session.EnqueueMessageEncrypted(housingProperties);
            else
                EnqueueToAll(housingProperties);
        }

        private void SendResidenceDecor(Player player = null)
        {
            var residenceDecor = new ServerHousingResidenceDecor();

            Decor[] decors = residence.GetDecor().ToArray();
            for (uint i = 0u; i < decors.Length; i++)
            {
                // client freaks out if too much decor is sent in a single message, limit to 100
                if (i != 0u && i % 100u == 0u)
                {
                    if (player != null)
                        player.Session.EnqueueMessageEncrypted(residenceDecor);
                    else
                        EnqueueToAll(residenceDecor);

                    residenceDecor = new ServerHousingResidenceDecor();
                }

                Decor decor = decors[i];
                residenceDecor.DecorData.Add(new ServerHousingResidenceDecor.Decor
                {
                    RealmId = WorldServer.RealmId,
                    DecorId = decor.DecorId,
                    ResidenceId = residence.Id,
                    DecorType = decor.Type,
                    PlotIndex = decor.PlotIndex,
                    Scale = decor.Scale,
                    Position = decor.Position,
                    Rotation = decor.Rotation,
                    DecorInfoId = decor.Entry.Id,
                    ParentDecorId = decor.DecorParentId,
                    ColourShift = decor.ColourShiftId
                });

                if (i == decors.Length - 1)
                {
                    if (player != null)
                        player.Session.EnqueueMessageEncrypted(residenceDecor);
                    else
                        EnqueueToAll(residenceDecor);
                }
            }
        }

        /// <summary>
        /// Crate all placed <see cref="Decor"/>, this is called directly from a packet hander.
        /// </summary>
        public void CrateAllDecor(Player player)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            var housingResidenceDecor = new ServerHousingResidenceDecor();
            foreach (Decor decor in residence.GetPlacedDecor())
            {
                decor.Crate();

                housingResidenceDecor.DecorData.Add(new ServerHousingResidenceDecor.Decor
                {
                    RealmId = WorldServer.RealmId,
                    DecorId = decor.DecorId,
                    ResidenceId = residence.Id,
                    DecorType = decor.Type,
                    PlotIndex = decor.PlotIndex,
                    Scale = decor.Scale,
                    Position = decor.Position,
                    Rotation = decor.Rotation,
                    DecorInfoId = decor.Entry.Id
                });
            }

            EnqueueToAll(housingResidenceDecor);
        }

        /// <summary>
        /// Update <see cref="Decor"/> (create, move or delete), this is called directly from a packet hander.
        /// </summary>
        public void DecorUpdate(Player player, ClientHousingDecorUpdate housingDecorUpdate)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            foreach (ClientHousingDecorUpdate.DecorUpdate update in housingDecorUpdate.DecorUpdates)
            {
                switch (housingDecorUpdate.Operation)
                {
                    case DecorUpdateOperation.Create:
                        DecorCreate(player, update);
                        break;
                    case DecorUpdateOperation.Move:
                        DecorMove(player, update);
                        break;
                    case DecorUpdateOperation.Delete:
                        DecorDelete(update);
                        break;
                    default:
                        throw new InvalidPacketValueException();
                }
            }
        }

        /// <summary>
        /// Create and add <see cref="Decor"/> from supplied <see cref="HousingDecorInfoEntry"/> to your crate.
        /// </summary>
        public void DecorCreate(HousingDecorInfoEntry entry, uint quantity)
        {
            var residenceDecor = new ServerHousingResidenceDecor();
            for (uint i = 0u; i < quantity; i++)
            {
                Decor decor = residence.DecorCreate(entry);
                decor.Type = DecorType.Crate;

                residenceDecor.DecorData.Add(new ServerHousingResidenceDecor.Decor
                {
                    RealmId = WorldServer.RealmId,
                    DecorId = decor.DecorId,
                    ResidenceId = residence.Id,
                    DecorType = decor.Type,
                    PlotIndex = decor.PlotIndex,
                    Scale = decor.Scale,
                    Position = decor.Position,
                    Rotation = decor.Rotation,
                    DecorInfoId = decor.Entry.Id
                });
            }

            EnqueueToAll(residenceDecor);
        }

        private void DecorCreate(Player player, ClientHousingDecorUpdate.DecorUpdate update)
        {
            HousingDecorInfoEntry entry = GameTableManager.Instance.HousingDecorInfo.GetEntry(update.DecorInfoId);
            if (entry == null)
                throw new InvalidPacketValueException();

            if (entry.CostCurrencyTypeId != 0u && entry.Cost != 0u)
            {
                /*if (!player.CurrencyManager.CanAfford((byte)entry.CostCurrencyTypeId, entry.Cost))
                {
                    // TODO: show error
                    return;
                }

                player.CurrencyManager.CurrencySubtractAmount((byte)entry.CostCurrencyTypeId, entry.Cost);*/
            }

            Decor decor = residence.DecorCreate(entry);
            decor.Type = update.DecorType;

            if (update.ColourShiftId != decor.ColourShiftId)
            {
                if (update.ColourShiftId != 0u)
                {
                    ColorShiftEntry colourEntry = GameTableManager.Instance.ColorShift.GetEntry(update.ColourShiftId);
                    if (colourEntry == null)
                        throw new InvalidPacketValueException();
                }

                decor.ColourShiftId = update.ColourShiftId;
            }

            if (update.DecorType != DecorType.Crate)
            {
                if (update.Scale < 0f)
                    throw new InvalidPacketValueException();

                // new decor is being placed directly in the world
                decor.Position = update.Position;
                decor.Rotation = update.Rotation;
                decor.Scale = update.Scale;
            }

            EnqueueToAll(new ServerHousingResidenceDecor
            {
                Operation = 0,
                DecorData = new List<ServerHousingResidenceDecor.Decor>
                {
                    new ServerHousingResidenceDecor.Decor
                    {
                        RealmId     = WorldServer.RealmId,
                        DecorId     = decor.DecorId,
                        ResidenceId = residence.Id,
                        DecorType   = decor.Type,
                        PlotIndex   = decor.PlotIndex,
                        Scale       = decor.Scale,
                        Position    = decor.Position,
                        Rotation    = decor.Rotation,
                        DecorInfoId = decor.Entry.Id,
                        ColourShift = decor.ColourShiftId
                    }
                }
            });
        }

        private void DecorMove(Player player, ClientHousingDecorUpdate.DecorUpdate update)
        {
            Decor decor = residence.GetDecor(update.DecorId);
            if (decor == null)
                throw new InvalidPacketValueException();

            HousingResult GetResult()
            {
                if (!IsValidPlotForPosition(update))
                    return HousingResult.Decor_InvalidPosition;

                if (update.ColourShiftId != 0u)
                {
                    ColorShiftEntry colourEntry = GameTableManager.Instance.ColorShift.GetEntry(update.ColourShiftId);
                    if (colourEntry == null)
                        throw new InvalidPacketValueException();
                }

                return HousingResult.Success;
            }

            HousingResult result = GetResult();
            if (result == HousingResult.Success)
            {
                if (update.PlotIndex != decor.PlotIndex)
                {
                    decor.PlotIndex = update.PlotIndex;
                }

                if (update.ColourShiftId != decor.ColourShiftId)
                {
                    if (update.ColourShiftId != 0u)
                    {
                        ColorShiftEntry colourEntry = GameTableManager.Instance.ColorShift.GetEntry(update.ColourShiftId);
                        if (colourEntry == null)
                            throw new InvalidPacketValueException();
                    }

                    decor.ColourShiftId = update.ColourShiftId;
                }

                if (decor.Type == DecorType.Crate)
                {
                    if (decor.Entry.Creature2IdActiveProp != 0u)
                    {
                        // TODO: used for decor that have an associated entity
                    }

                    // crate->world
                    decor.Move(update.DecorType, update.Position, update.Rotation, update.Scale);
                }
                else
                {
                    if (update.DecorType == DecorType.Crate)
                        decor.Crate();
                    else
                    {
                        // world->world
                        decor.Move(update.DecorType, update.Position, update.Rotation, update.Scale);
                        decor.DecorParentId = update.ParentDecorId;
                    }
                }

                EnqueueToAll(new ServerHousingResidenceDecor
                {
                    Operation = 0,
                    DecorData = new List<ServerHousingResidenceDecor.Decor>
                    {
                        new ServerHousingResidenceDecor.Decor
                        {
                            RealmId       = WorldServer.RealmId,
                            DecorId       = decor.DecorId,
                            ResidenceId   = residence.Id,
                            DecorType     = decor.Type,
                            PlotIndex     = decor.PlotIndex,
                            Scale         = decor.Scale,
                            Position      = decor.Position,
                            Rotation      = decor.Rotation,
                            DecorInfoId   = decor.Entry.Id,
                            ParentDecorId = decor.DecorParentId,
                            ColourShift   = decor.ColourShiftId
                        }
                    }
                });
            }
            else
            {
                player.Session.EnqueueMessageEncrypted(new ServerHousingResult
                {
                    RealmId = WorldServer.RealmId,
                    ResidenceId = residence.Id,
                    PlayerName = player.Name,
                    Result = result
                });
            }
        }

        private void DecorDelete(ClientHousingDecorUpdate.DecorUpdate update)
        {
            Decor decor = residence.GetDecor(update.DecorId);
            if (decor == null)
                throw new InvalidPacketValueException();

            if (decor.Position != Vector3.Zero)
                throw new InvalidOperationException();

            residence.DecorDelete(decor);

            // TODO: send packet to remove from decor list
        }

        /// <summary>
        /// Used to confirm the position and PlotIndex are valid together when placing Decor
        /// </summary>
        private bool IsValidPlotForPosition(ClientHousingDecorUpdate.DecorUpdate update)
        {
            if (update.PlotIndex == int.MaxValue)
                return true;

            WorldSocketEntry worldSocketEntry = GameTableManager.Instance.WorldSocket.GetEntry(residence.GetPlot((byte)update.PlotIndex).PlotEntry.WorldSocketId);

            // TODO: Calculate position based on individual maps on Community & Warplot residences
            Vector3 worldPosition = new Vector3(1472f + update.Position.X, update.Position.Y, 1440f + update.Position.Z);

            (uint gridX, uint gridZ) = MapGrid.GetGridCoord(worldPosition);
            (uint localCellX, uint localCellZ) = MapCell.GetCellCoord(worldPosition);
            (uint globalCellX, uint globalCellZ) = (gridX * MapDefines.GridCellCount + localCellX, gridZ * MapDefines.GridCellCount + localCellZ);

            // TODO: Investigate need for offset.
            // Offset added due to calculation being +/- 2 sometimes when placing very close to plots. They were valid placements in the client, though.
            uint maxBound = worldSocketEntry.BoundIds.Max() + 2;
            uint minBound = worldSocketEntry.BoundIds.Min() - 2;

            log.Debug($"IsValidPlotForPosition - PlotIndex: {update.PlotIndex}, Range: {minBound}-{maxBound}, Coords: {globalCellX}, {globalCellZ}");

            return (globalCellX >= minBound && globalCellX <= maxBound && globalCellZ >= minBound && globalCellZ <= maxBound);
        }

        /// <summary>
        /// Rename <see cref="Residence"/>, this is called directly from a packet hander.
        /// </summary>
        public void Rename(Player player, ClientHousingRenameProperty housingRenameProperty)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            residence.Name = housingRenameProperty.Name;
            SendHousingProperties();
        }

        /// <summary>
        /// Set <see cref="ResidencePrivacyLevel"/>, this is called directly from a packet hander.
        /// </summary>
        public void SetPrivacyLevel(Player player, ClientHousingSetPrivacyLevel housingSetPrivacyLevel)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            if (housingSetPrivacyLevel.PrivacyLevel == ResidencePrivacyLevel.Public)
                ResidenceManager.Instance.RegisterResidenceVists(residence.Id, residence.OwnerName, residence.Name);
            else
                ResidenceManager.Instance.DeregisterResidenceVists(residence.Id);

            residence.PrivacyLevel = housingSetPrivacyLevel.PrivacyLevel;
            SendHousingPrivacy();
        }

        /// <summary>
        /// Remodel <see cref="Residence"/>, this is called directly from a packet hander.
        /// </summary>
        public void Remodel(Player player, ClientHousingRemodel housingRemodel)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            if (housingRemodel.RoofDecorInfoId != 0u)
                residence.Roof = (ushort)housingRemodel.RoofDecorInfoId;
            if (housingRemodel.WallpaperId != 0u)
                residence.Wallpaper = (ushort)housingRemodel.WallpaperId;
            if (housingRemodel.EntrywayDecorInfoId != 0u)
                residence.Entryway = (ushort)housingRemodel.EntrywayDecorInfoId;
            if (housingRemodel.DoorDecorInfoId != 0u)
                residence.Door = (ushort)housingRemodel.DoorDecorInfoId;
            if (housingRemodel.SkyWallpaperId != 0u)
                residence.Sky = (ushort)housingRemodel.SkyWallpaperId;
            if (housingRemodel.MusicId != 0u)
                residence.Music = (ushort)housingRemodel.MusicId;
            if (housingRemodel.GroundWallpaperId != 0u)
                residence.Ground = (ushort)housingRemodel.GroundWallpaperId;

            SendHousingProperties();
        }

        /// <summary>
        /// Sends <see cref="ServerHousingPlots"/> to the player
        /// </summary>
        private void SendHousingPlots(Player player = null)
        {
            var housingPlots = new ServerHousingPlots
            {
                RealmId = WorldServer.RealmId,
                ResidenceId = residence.Id,
            };

            foreach (Plot plot in residence.GetPlots())
            {
                if (player != null && plot.PlugEntry != null && plot.BuildState != 4)
                    plot.BuildState = 4;

                housingPlots.Plots.Add(new ServerHousingPlots.Plot
                {
                    PlotPropertyIndex = plot.Index,
                    PlotInfoId = plot.PlotEntry.Id,
                    PlugFacing = plot.PlugFacing,
                    PlugItemId = plot.PlugEntry?.Id ?? 0u,
                    BuildState = plot.BuildState
                });
            }

            if (player != null)
                player.Session.EnqueueMessageEncrypted(housingPlots);
            else
                EnqueueToAll(housingPlots);
        }

        /// <summary>
        /// Install a House Plug into a Plot
        /// </summary>
        private void SetHousePlug(Player player, ClientHousingPlugUpdate housingPlugUpdate, HousingPlugItemEntry plugItemEntry)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            Plot plot = residence.GetPlot(housingPlugUpdate.PlotInfo);
            if (plot == null)
                throw new HousingException();

            // TODO: Confirm that this plug is usable in said slot

            // TODO: Figure out how the "Construction Yard" shows up. Appears to be related to time and not a specific packet. 
            //       Telling the client that the Plots were updated looks to be the only trigger for the building animation.

            // Update the Plot and queue necessary plug updates
            if (residence.SetHouse(plugItemEntry, this))
            {
                HandleHouseChange(player, plot, housingPlugUpdate);
            }
            else
                player.Session.EnqueueMessageEncrypted(new ServerHousingResult
                {
                    RealmId = WorldServer.RealmId,
                    ResidenceId = residence.Id,
                    PlayerName = player.Name,
                    Result = HousingResult.Plug_InvalidPlug
                });
        }

        /// <summary>
        /// Handles updating the client with changes following a 2x2 Plot change
        /// </summary>
        private void HandleHouseChange(Player player, Plot plot, ClientHousingPlugUpdate housingPlugUpdate = null)
        {
            // TODO: Crate all decor stored inside the house?

            UpdatePlot(player, plot, housingPlugUpdate);

            // Send updated Property data (informs residence, roof, door, etc.)
            SendHousingProperties();

            // Resend the Action Bar because buttons may've been enabled after adding a house
            player.Session.EnqueueMessageEncrypted(new ServerShowActionBar
            {
                ShortcutSet = ShortcutSet.FloatingSpellBar,
                ActionBarShortcutSetId = 1553,
                Guid = player.Guid
            });

            // Send plots again after Property was updated
            SendHousingPlots();

            // Send residence decor after house change
            SendResidenceDecor();

            // TODO: Run script(s) associated with PlugItem

            // Set plot to "Built", and inform all clients
            plot.BuildState = 4;
            // TODO: Move this to an Update method, and ensure any timers are honoured before build is completed
            EnqueueToAll(new ServerHousingPlotUpdate
            {
                RealmId = WorldServer.RealmId,
                ResidenceId = residence.Id,
                PlotIndex = plot.Index,
                BuildStage = 0,
                BuildState = plot.BuildState
            });
        }

        /// <summary>
        /// Install a Plug into a Plot; Should only be called on a client update.
        /// </summary>
        public void SetPlug(Player player, ClientHousingPlugUpdate housingPlugUpdate)
        {   
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            Plot plot = residence.GetPlot(housingPlugUpdate.PlotInfo);
            if (plot == null)
                throw new HousingException();

            HousingPlugItemEntry plugItemEntry = GameTableManager.Instance.HousingPlugItem.GetEntry(housingPlugUpdate.PlugItem);
            if (plugItemEntry == null)
                throw new InvalidPacketValueException();

            // TODO: Confirm that this plug is usable in said slot

            if (plot.Index == 0)
                SetHousePlug(player, housingPlugUpdate, plugItemEntry);
            else
            {
                // TODO: Figure out how the "Construction Yard" shows up. Appears to be related to time and not a specific packet. 
                //       Telling the client that the Plots were updated looks to be the only trigger for the building animation.

                // Update the Plot and queue necessary plug updates
                UpdatePlot(player, plot, housingPlugUpdate);

                SendHousingPlots();

                // TODO: Run script(s) associated with PlugItem

                // Set plot to "Built", and inform all clients
                plot.BuildState = 4;
                // TODO: Move this to an Update method, and ensure any timers are honoured before build is completed
                EnqueueToAll(new ServerHousingPlotUpdate
                {
                    RealmId = WorldServer.RealmId,
                    ResidenceId = residence.Id,
                    PlotIndex = plot.Index,
                    BuildStage = 0,
                    BuildState = plot.BuildState
                });
            }

            // TODO: Deduct any cost and/or items
        }

        /// <summary>
        /// Update supplied <see cref="Plot"/>; Should only be called by <see cref="ResidenceMap"/> when updating plot information
        /// </summary>
        private void UpdatePlot(Player player, Plot plot, ClientHousingPlugUpdate housingPlugUpdate = null)
        {
            // Set this Plot to use the correct Plug
            if (housingPlugUpdate != null)
                plot.SetPlug(housingPlugUpdate.PlugItem, (HousingPlugFacing)housingPlugUpdate.PlugFacing == HousingPlugFacing.Default ? HousingPlugFacing.East : (HousingPlugFacing)housingPlugUpdate.PlugFacing);

            SendHousingPlots();

            // TODO: Figure out what this packet is for
            player.Session.EnqueueMessageEncrypted(new Server051F
            {
                RealmId = WorldServer.RealmId,
                ResidenceId = residence.Id,
                PlotIndex = plot.Index
            });

            // Instatiate new plug entity and assign to Plot's PlugEntity cache
            var newPlug = new Plug(plot.PlotEntry, plot.PlugEntry);

            if (plot.PlugEntity != null)
                plot.PlugEntity.EnqueueReplace(newPlug);
            else
                EnqueueAdd(newPlug, Vector3.Zero);

            // Update plot with PlugEntity reference
            plot.SetPlugEntity(newPlug);
        }

        /// <summary>
        /// Updates <see cref="Plot"/> to have no plug installed; Should only be called on a client update.
        /// </summary>
        public void RemovePlug(Player player, ClientHousingPlugUpdate housingPlugUpdate)
        {
            if (!residence.CanModifyResidence(player.CharacterId))
                throw new InvalidPacketValueException();

            Plot plot = residence.GetPlot(housingPlugUpdate.PlotInfo);
            if (plot == null)
                throw new HousingException();

            RemovePlug(player, plot);
        }

        /// <summary>
        /// Updates <see cref="Plot"/> to have no plug installed
        /// </summary>
        private void RemovePlug(Player player, Plot plot)
        {
            // Handle changes if plot is the house plot
            if (plot.Index == 0)
                RemoveHouse(player, plot);
            else
            {
                plot.PlugEntity.RemoveFromMap();
                plot.RemovePlug();

                SendHousingPlots();
            }
        }

        /// <summary>
        /// Updates supplied <see cref="Plot"/> to have no house on it
        /// </summary>
        private void RemoveHouse(Player player, Plot plot)
        {
            if (plot.Index > 0)
                throw new ArgumentOutOfRangeException("plot.Index", "Plot Index must be 0 to remove a house");

            // Clear all House information from the Residence instance associated with this map
            residence.RemoveHouse();

            // Even when no house is being used, the plug must be set for the house otherwise clients will get stuck on load screen
            plot.SetPlug(18); // Defaults to Starter Tent

            HandleHouseChange(player, plot);
        }
    }
}
