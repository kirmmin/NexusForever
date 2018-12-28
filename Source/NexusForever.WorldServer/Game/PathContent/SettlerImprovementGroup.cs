using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.PathContent.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NexusForever.WorldServer.Game.PathContent
{
    public class SettlerImprovementGroup : IUpdate
    {
        public PathSettlerImprovementGroupEntry Entry { get; private set; }
        public List<PathSettlerImprovementEntry> Tiers { get; private set; }
        public UpdateTimer expiryTimer = new UpdateTimer(0, false);
        public bool PendingExpiry() => expiryTimer.HasElapsed;
        public bool Active { get; private set; }
        public int Tier { get; private set; } = -1;
        public List<string> Owners { get; set; } = new List<string>();
        public WorldEntity Entity { get; private set; }

        public SettlerImprovementGroup(uint groupId)
        {
            Entry = GameTableManager.Instance.PathSettlerImprovementGroup.GetEntry(groupId);
            Tiers = GameTableManager.Instance.PathSettlerImprovement.Entries.Where(i => Entry.PathSettlerImprovementTiers.Contains(i.Id)).ToList();
        }

        public void Update(double lastTick)
        {
            if (expiryTimer.IsTicking)
            {
                expiryTimer.Update(lastTick);

                if (expiryTimer.HasElapsed)
                {
                    // Destroy Entity and stuff
                    Entity.RemoveFromMap();
                    Active = false;
                    Tier = -1;
                    Owners.Clear();
                }
            }
        }

        public void Build(Player player, int tier)
        {
            if (tier > 0)
                throw new NotImplementedException();

            Active = true;
            Tier = tier;
            expiryTimer = new UpdateTimer(expiryTimer.Time + Entry.DurationPerBundleMs / 1000d, true);

            // Charge user

            // Update quest progress
            player.PathMissionManager.MissionUpdate(PathMissionType.Settler_Hub, Entry.PathSettlerHubId, 1u);

            Owners.Add(player.Name);
            player.EnqueueToVisible(new ServerSettlerBuildStatus
            {
                HubId = (ushort)Entry.PathSettlerHubId,
                ImprovementGroup = GetNetworkBuildStatus()
            }, true);
            player.EnqueueToVisible(new ServerSettlerHubUpdate
            {
                HubId = (ushort)Entry.PathSettlerHubId
            }, true);
            player.Session.EnqueueMessageEncrypted(new ServerSettlerBuildResult
            {
                Result = 1,
                ImprovementId = (ushort)Tiers[0].Id,
                GroupId = (ushort)Entry.Id
            });

            ImprovementInfo info = GlobalPathContentManager.Instance.GetImprovementInfo(Entry.Id);

            Entity = new Simple(info.CreatureId, Entry.Id, info.DisplayInfo);
            player.Map.EnqueueAdd(Entity, info.Position);
        }

        /// <summary>
        /// Returns a <see cref="ServerSettlerHubStatus.ImprovementGroup"/> to be sent to a <see cref="Player"/>
        /// </summary>
        /// <returns></returns>
        public SettlerImprovementGroupStatus GetNetworkBuildStatus()
        {
            return new SettlerImprovementGroupStatus
            {
                GroupId = (ushort)Entry.Id,
                BundleCount = Active ? (uint)(expiryTimer.Time * 1000d / Entry.DurationPerBundleMs) : 0,
                RemainingMs = Active ? (uint)(expiryTimer.Time * 1000d) : 0,
                Tier = Tier
            };
        }
    }
}
