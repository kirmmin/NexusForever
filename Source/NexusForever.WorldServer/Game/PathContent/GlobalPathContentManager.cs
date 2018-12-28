using NexusForever.Shared;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PathContent.Static;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NexusForever.WorldServer.Game.PathContent
{
    public class GlobalPathContentManager : Singleton<GlobalPathContentManager>, IUpdate
    {
        // TODO: Do we need to save Improvement Group State during Server Reboot/Crash?
        private readonly Dictionary</* groupId */ uint, SettlerImprovementGroup> settlerImprovementGroups = new Dictionary<uint, SettlerImprovementGroup>();
        private readonly Dictionary</* groupId */ uint, ImprovementInfo> improvementInfo = new Dictionary<uint, ImprovementInfo>();

        public GlobalPathContentManager()
        {
        }

        public void Initialise()
        {
            //improvementInfo.Add(468, new ImprovementInfo()); // Royal Collegium
            improvementInfo.Add(469, new ImprovementInfo(new Vector3(-3480.7605f, -976.58154f, -6060.8135f), 25867, 25593)); // Shield Capacity Booster
            improvementInfo.Add(470, new ImprovementInfo(new Vector3(-3473f, -977.09224f, -6070.1f), 25716, 25593)); // Physical Resistance
            improvementInfo.Add(2620, new ImprovementInfo(new Vector3(-3489.43f, -976.972f, -6074.117f), 18838, 25612)); // Bank
        }


        public void Update(double lastTick)
        {
            foreach (SettlerImprovementGroup group in settlerImprovementGroups.Values)
                group.Update(lastTick);
        }

        public ImprovementInfo GetImprovementInfo(uint groupId)
        {
            return improvementInfo.TryGetValue(groupId, out ImprovementInfo info) ? info : null;
        }


        public void OnEnterZone(Player player)
        {
            switch (player.Path)
            {
                case Path.Settler:
                    SettlerSendImprovementBuildStatus(player);
                    break;
            }
        }

        public void OnEntityInteract(Player player, WorldEntity target, uint interactionEvent)
        {
            switch (interactionEvent)
            {
                case 30:
                    if (player.Path != Path.Settler)
                        return;

                    List<PathSettlerImprovementGroupEntry> improvementGroups = GameTableManager.Instance.PathSettlerImprovementGroup.Entries.Where(i => i.Creature2IdDepot == target.CreatureId).ToList();
                    if (improvementGroups.Count == 0)
                        return;

                    PathSettlerHubEntry hub = GameTableManager.Instance.PathSettlerHub.GetEntry(improvementGroups.First().PathSettlerHubId);

                    var buildStatus = new ServerSettlerHubStatus
                    {
                        HubId = (ushort)hub.Id
                    };

                    foreach (PathSettlerImprovementGroupEntry groupEntry in GameTableManager.Instance.PathSettlerImprovementGroup.Entries.Where(g => g.PathSettlerHubId == hub.Id))
                    {
                        if (!settlerImprovementGroups.ContainsKey(groupEntry.Id))
                            settlerImprovementGroups.Add(groupEntry.Id, new SettlerImprovementGroup(groupEntry.Id));

                        buildStatus.ImprovementGroups.Add(settlerImprovementGroups[groupEntry.Id].GetNetworkBuildStatus());
                    }

                    player.Session.EnqueueMessageEncrypted(buildStatus);
                    player.Session.EnqueueMessageEncrypted(new ServerSettlerImprovementGroups
                    {
                        UnitId = target.Guid,
                        Groups = improvementGroups.Select(i => i.Id).ToList()
                    });
                    break;
            }
        }

        public void OnAddVisible(Player player, Simple target)
        {
            player.Session.EnqueueMessageEncrypted(new ServerSettlerImprovementInfo
            {
                UnitId = target.Guid,
                GroupId = (ushort)target.ImprovementGroupId,
                RemainingMs = (uint)(settlerImprovementGroups[target.ImprovementGroupId].expiryTimer.Time * 1000d),
                Owners = settlerImprovementGroups[target.ImprovementGroupId].Owners,
                Tiers = settlerImprovementGroups[target.ImprovementGroupId].Owners.Select(a => new ServerSettlerImprovementInfo.TierInfo
                {
                    Name = a,
                    Tier = 0
                }).ToList()
            });
        }

        /// <summary>
        /// Builds the selected Settler Improvement. Should only be called directly from Packet Handler.
        /// </summary>
        public void HandleExplorerPlaceSignal(Player player, ClientExplorerPlaceSignal placeSignal)
        {
            PathMissionEntry missionEntry = GameTableManager.Instance.PathMission.GetEntry(placeSignal.MissionId);
            if (missionEntry == null)
                throw new InvalidOperationException($"Mission ID not found for ExplorerPlaceSignal: {placeSignal.MissionId}");

            Vector3 signalPosition;

            switch ((PathMissionType)missionEntry.PathMissionTypeEnum)
            {
                case PathMissionType.Explorer_Vista:
                    PathExplorerNodeEntry explorerNode = GameTableManager.Instance.PathExplorerNode.Entries.FirstOrDefault(i => i.PathExplorerAreaId == missionEntry.ObjectId);
                    if (explorerNode == null)
                        throw new InvalidOperationException($"ExplorerNode with ID {missionEntry.ObjectId} not found!");

                    WorldLocation2Entry signalLocation = GameTableManager.Instance.WorldLocation2.GetEntry(explorerNode.WorldLocation2Id);
                    if (signalLocation == null)
                        throw new InvalidOperationException($"WorldLocation2 with ID {explorerNode.WorldLocation2Id} not found!");

                    signalPosition = new Vector3(signalLocation.Position0, signalLocation.Position1, signalLocation.Position2);

                    // Update Achievements

                    // Update Quest Progress
                    player.PathMissionManager.MissionUpdate(PathMissionType.Explorer_Vista, missionEntry.ObjectId);
                    break;
                default:
                    throw new NotImplementedException($"{(PathMissionType)missionEntry.PathMissionTypeEnum} not supported at this time.");
            }

            // TODO: Place Signal Entity (ID: 12047 | CreateFlags: 1)
            if (signalPosition != null)
            {
                Simple signal = new Simple(GameTableManager.Instance.Creature2.GetEntry(12047));
                signal.CreateFlags = EntityCreateFlag.SpawnAnimation;
                signal.SetDisplayInfo(23011);
                player.Map.EnqueueAdd(signal, signalPosition);
            }

            // Play Cinematic for Mission Complete
        }

        /// <summary>
        /// Builds the selected Settler Improvement. Should only be called directly from Packet Handler.
        /// </summary>
        public void HandleSettlerBuildImprovement(Player player, ClientSettlerBuildImprovement buildImprovement)
        {
            if (!settlerImprovementGroups.TryGetValue(buildImprovement.GroupId, out SettlerImprovementGroup group))
                throw new InvalidOperationException($"SettlerImprovementGroup {buildImprovement.GroupId} doesn't exist, but it should!");

            group.Build(player, buildImprovement.Tier);
        }

        private void SettlerSendImprovementBuildStatus(Player player)
        {
            foreach (PathMission mission in player.PathMissionManager.GetActiveEpisodeMissions())
            {
                if ((Path)mission.Entry.PathTypeEnum != Path.Settler)
                    continue;

                if ((PathMissionType)mission.Entry.PathMissionTypeEnum != PathMissionType.Settler_Hub)
                    continue;

                PathSettlerHubEntry hub = GameTableManager.Instance.PathSettlerHub.GetEntry(mission.Entry.ObjectId);
                if (hub == null)
                    continue;

                var buildStatus = new ServerSettlerHubStatus
                {
                    HubId = (ushort)hub.Id
                };

                foreach (PathSettlerImprovementGroupEntry groupEntry in GameTableManager.Instance.PathSettlerImprovementGroup.Entries.Where(g => g.PathSettlerHubId == hub.Id))
                {
                    if (!settlerImprovementGroups.ContainsKey(groupEntry.Id))
                        settlerImprovementGroups.Add(groupEntry.Id, new SettlerImprovementGroup(groupEntry.Id));

                    buildStatus.ImprovementGroups.Add(settlerImprovementGroups[groupEntry.Id].GetNetworkBuildStatus());
                }

                player.Session.EnqueueMessageEncrypted(buildStatus);
            }
        }
    }
}
