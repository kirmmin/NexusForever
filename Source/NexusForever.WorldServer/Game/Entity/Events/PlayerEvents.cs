using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.GameTable.Static;
using NexusForever.WorldServer.Game.Cinematic.Cinematics;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.Quest.Static;
using NexusForever.WorldServer.Game.Social;
using NexusForever.WorldServer.Game.Social.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Script;
using System;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public partial class Player
    {
        private float perSecondTick;
        private double medicTickTimer;
        private float medicRefillRate = 4f;
        private float spellPowerRefillRate = 4f;
        private double warriorBuilderTimer;
        private float warriorDecayRate = 150f;
        private float esperResetTimer;
        private float engineerResetTimer;

        private bool ClientReadyToEnter = false;
        private bool ServerReadyToEnter = false;

        private void OnLogin()
        {
            string motd = WorldServer.RealmMotd;
            if (motd?.Length > 0)
                GlobalChatManager.Instance.SendMessage(Session, motd, "MOTD", ChatChannelType.Realm);

            GuildManager.OnLogin();
            ChatManager.OnLogin();
            ContactManager.OnLogin();
            ShutdownManager.Instance.OnLogin(this);
            GlobalChatManager.Instance.JoinDefaultChatChannels(this);
        }

        private void OnLogout()
        {
            GuildManager.OnLogout();
            ChatManager.OnLogout();
            GlobalChatManager.Instance.LeaveDefaultChatChannels(this);
        }

        public override void OnEnqueueAddToMap(MapPosition mapPosition)
        {
            ServerReadyToEnter = false;
            IsLoading = true;

            CreateFlags &= ~EntityCreateFlag.NoSpawnAnimation;
            CreateFlags |= EntityCreateFlag.SpawnAnimation;

            Session.EnqueueMessageEncrypted(new ServerChangeWorld
            {
                WorldId = (ushort)mapPosition.Info.Entry.Id,
                Position = new Position(mapPosition.Position),
                Yaw = Rotation.X
            });

            base.OnEnqueueAddToMap(mapPosition);
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            ServerReadyToEnter = true;

            Guid     = guid;
            Map      = map;
            Position = vector;
            MovementManager = new Movement.MovementManager(this, vector, Rotation);

            if (!ClientReadyToEnter)
                return;
            // If PreviousMap is null, we need to wait for client started loading packet before sending data.
            
            HandleEnteringWorld();
        }

        public void HandleEnteringWorld()
        {
            log.Info("Calling HandleEnteringWorld");

            ClientReadyToEnter = true;

            if (!ServerReadyToEnter)
                return;

            // TODO: If player is logging in to this character for first time, we should wait for confirmation from client that it's in loading screen before dumping all packets to it.
            // Currently, dumping the packets to the client works fine, but seems to have a higher chance of crashing or bugging up.

            // Send Packets for Player, its Entity, and all Map, Character, and Account Data
            SendPacketsOnAddToMap();

            // TODO: May be better not calling the base Classes on this, especially when logging into character from character select, when we should wait for client confirmation that it's ready for data.
            // Send all Map Entities
            UpdateEntities();

            // Send all Packets that wrap up Entities and inform client about any final Map Settings
            SendPacketsAfterEntities();

            pendingTeleport = null;

            if (Health == 0u)
                SetDeathState(DeathState.JustDied);
            else
                SetDeathState(DeathState.JustSpawned);

            if (PreviousMap == null)
                OnLogin();
        }

        private void UpdateEntities()
        {
            UpdateVision();
            UpdateGridVision();
        }

        /// <summary>
        /// This handles informing the client of any final pieces of information and passing them control of their character. Only to be called after receiving <see cref="ClientEnteredWorld"/>.
        /// </summary>
        public void OnEnterWorld()
        {
            // Send Cinematics Data for Load In (likely indicates that scripts were executed at this point)
            ScriptManager.Instance.GetScript<MapScript>(Map.Entry.Id)?.OnAddToMap(this);
            // TODO: Move this to a script
            if (Map.Entry.Id == 3460 && firstTimeLoggingIn)
                CinematicManager.QueueCinematic(new NoviceTutorialOnEnter(this));

            // 0x0091
            // Passive Spells Start for Player

            // 0x0636 ServerUnitControlSet
            SetControl(this);

            // Multiple sets of following 3 packets are sent. These appear to be all related to events, specifically holiday events.
            // 0x0700
            // 0x0112
            // 0x0135

            // 0x0061 ServerPlayerEnteredWorld
            // This allows player to exit loading screen
            Session.EnqueueMessageEncrypted(new ServerPlayerEnteredWorld());
            IsLoading = false;
        }

        public override void OnRemoveFromMap()
        {
            DestroyDependents();

            base.OnRemoveFromMap();
        }

        public override void OnRelocate(Vector3 vector)
        {
            base.OnRelocate(vector);
            saveMask |= PlayerSaveMask.Location;

            ZoneMapManager.OnRelocate(vector);
        }

        protected override void OnZoneUpdate(WorldZoneEntry oldZone)
        {
            if (oldZone != null && oldZone != Zone)
                PreviousZone = oldZone;

            if (Zone != null)
            {
#if DEBUG
                TextTable tt = GameTableManager.Instance.GetTextTable(Language.English);
                if (tt != null)
                {
                    GlobalChatManager.Instance.SendMessage(Session, $"New Zone: ({Zone.Id}){tt.GetEntry(Zone.LocalizedTextIdName)}");
                }
#endif

                uint tutorialId = AssetManager.Instance.GetTutorialIdForZone(Zone.Id);
                if (tutorialId > 0)
                {
                    Session.EnqueueMessageEncrypted(new ServerTutorial
                    {
                        TutorialId = tutorialId
                    });
                }

                QuestManager.ObjectiveUpdate(QuestObjectiveType.EnterZone, Zone.Id, 1);
                ScriptManager.Instance.GetScript<MapScript>(Map.Entry.Id)?.OnEnterZone(this, Zone.Id);
            }

            ZoneMapManager.OnZoneUpdate();
        }

        /// <summary>
        /// Fires every time a regeneration tick occurs (every 0.5s)
        /// </summary>
        protected override void OnTickRegeneration()
        {
            base.OnTickRegeneration();

            perSecondTick += 0.5f;

            float resource3Remaining = GetStatFloat(Stat.Resource3) ?? 0f;
            if (Class == Class.Stalker && resource3Remaining < GetPropertyValue(Property.ResourceMax3))
            {
                float resource3RegenAmount = GetPropertyValue(Property.ResourceMax3) * GetPropertyValue(Property.ResourceRegenMultiplier3);
                SetStat(Stat.Resource3, (float)Math.Min(resource3Remaining + resource3RegenAmount, (float)GetPropertyValue(Property.ResourceMax3)));
            }

            float enduranceRemaining = GetStatFloat(Stat.Resource0) ?? 0f;
            if (enduranceRemaining < GetPropertyValue(Property.ResourceMax0))
            {
                float enduranceRegenAmount = GetPropertyValue(Property.ResourceMax0) * GetPropertyValue(Property.ResourceRegenMultiplier0);
                Endurance += enduranceRegenAmount;
            }

            float dashRemaining = GetStatFloat(Stat.Dash) ?? 0f;
            if (dashRemaining < GetPropertyValue(Property.ResourceMax7))
            {
                float dashRegenAmount = GetPropertyValue(Property.ResourceMax7) * GetPropertyValue(Property.ResourceRegenMultiplier7);
                SetStat(Stat.Dash, (float)Math.Min(dashRemaining + dashRegenAmount, (float)GetPropertyValue(Property.ResourceMax7)));
            }

            float focusRemaining = GetStatFloat(Stat.Focus) ?? 0f;
            if (perSecondTick >= 1f && focusRemaining < GetPropertyValue(Property.BaseFocusPool))
            {
                float focusRegenAmount = GetPropertyValue(Property.BaseFocusPool) * (InCombat ? GetPropertyValue(Property.BaseFocusRecoveryInCombat) : GetPropertyValue(Property.BaseFocusRecoveryOutofCombat));
                Focus += focusRegenAmount;
            }

            switch (Class)
            {
                case Class.Warrior:
                    if (InCombat)
                    {
                        warriorBuilderTimer += 0.5f;
                        if (warriorBuilderTimer > 3d)
                        {
                            if (Resource1 > 0f)
                                Resource1 -= (float)(warriorDecayRate * 0.5f);

                            if (Resource1 < float.Epsilon)
                                warriorBuilderTimer = 0f;
                        }
                    }
                    else
                    {
                        if (Resource1 > 0f)
                            Resource1 -= (float)(warriorDecayRate * 0.5f);
                    }
                    break;
                case Class.Spellslinger:
                    if (Resource4 < GetPropertyValue(Property.ResourceMax4))
                        Resource4 += (float)(spellPowerRefillRate * 0.5f);
                    break;
                case Class.Medic:
                    if (!InCombat)
                    {
                        medicTickTimer += 0.5f;
                        if (medicTickTimer > 1d)
                        {
                            if (Resource1 < GetPropertyValue(Property.ResourceMax1))
                                Resource1 += medicRefillRate;
                            medicTickTimer = 0d;
                        }
                    }
                    break;
                case Class.Esper:
                    if (!InCombat)
                    {
                        esperResetTimer += 0.5f;
                        if (esperResetTimer >= 10d)
                        {
                            Resource1 = 0f;
                            esperResetTimer = 0f;
                        }
                    }
                    else
                    {
                        if (esperResetTimer > 0f)
                            esperResetTimer = 0f;
                    }
                    break;
                case Class.Engineer:
                    if (!InCombat)
                    {
                        engineerResetTimer += 0.5f;
                        if (engineerResetTimer < 5f)
                            break;

                        if (perSecondTick >= 1f)
                            Resource1 -= 10f;
                    }
                    else
                    {
                        if (engineerResetTimer > 0f)
                            engineerResetTimer = 0f;
                    }
                    break;
            }

            if (perSecondTick >= 1f)
                perSecondTick = 0f;
        }

        protected override void OnStatChange(Stat stat, float newVal, float previousVal)
        {
            base.OnStatChange(stat, newVal, previousVal);

            switch (stat)
            {
                case Stat.Resource1:
                    if (Class == Class.Warrior && newVal >= previousVal)
                        warriorBuilderTimer = 0f;
                    break;
            }
        }

        protected override void OnDeath(UnitEntity killer)
        {
            base.OnDeath(killer);
        }

        public override void OnCombatStateChange(bool inCombat)
        {
            base.OnCombatStateChange(inCombat);
            
            HandleMovementSpeedApply();
        }
    }
}
