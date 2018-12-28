using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PathContent;
using NexusForever.WorldServer.Game.PathContent.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;

namespace NexusForever.WorldServer.Game.Entity
{
    public class PathMissionManager : ISaveCharacter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly Player player;
        private readonly Dictionary</*episodeId*/uint, PathEpisode> episodes = new Dictionary<uint, PathEpisode>();

        private uint currentEpisode = 0;

        /// <summary>
        /// Create a new <see cref="PathMissionManager"/> from <see cref="Player"/> database model.
        /// </summary>
        public PathMissionManager(Player owner, CharacterModel model)
        {
            player = owner;

            foreach (var characterPathEpisode in model.PathEpisode)
                episodes.Add(characterPathEpisode.EpisodeId, new PathEpisode(characterPathEpisode, player));

            //TODO: Check that all missions for each episode are accounted for and add missing ones. Need to do this in case DB crashes mid-write.
        }

        /// <summary>
        /// Create a new <see cref="CharacterPathEpisodeModel"/>.
        /// </summary>
        public PathEpisode PathEpisodeCreate(uint episodeId)
        {
            PathEpisodeEntry pathEpisodeEntry = GameTableManager.Instance.PathEpisode.GetEntry(episodeId);
            if (pathEpisodeEntry == null)
                return null;

            return PathEpisodeCreate(pathEpisodeEntry);
        }

        /// <summary>
        /// Create a new <see cref="CharacterPathEpisodeModel"/>.
        /// </summary>
        public PathEpisode PathEpisodeCreate(PathEpisodeEntry pathEpisodeEntry)
        {
            if (pathEpisodeEntry == null)
                throw new ArgumentNullException(nameof(pathEpisodeEntry));

            if (episodes.ContainsKey(pathEpisodeEntry.Id))
                throw new InvalidOperationException($"Path Episode {pathEpisodeEntry.Id} is already added to the player!");

            PathEpisode pathEpisode = new PathEpisode(
                player,
                pathEpisodeEntry
            );
            episodes.Add(pathEpisode.Id, pathEpisode);
            return pathEpisode;
        }

        /// <summary>
        /// Returns all <see cref="PathMission"/> for a given Path Episode ID
        /// </summary>
        public IEnumerable<PathMission> GetEpisodeMissions(uint pathEpisodeId)
        {
            if (pathEpisodeId <= 0)
                return null;

            return episodes.TryGetValue(pathEpisodeId, out PathEpisode value) ? value : Enumerable.Empty<PathMission>();
        }

        public IEnumerable<PathMission> GetActiveEpisodeMissions()
        {
            if (currentEpisode == 0)
                return Enumerable.Empty<PathMission>();

            return GetEpisodeMissions(currentEpisode);
        }

        /// <summary>
        /// Initiates necessary methods for when a player loads into the game. Must be called after entity has been created.
        /// </summary>
        public void SendInitialPackets()
        {
            SetEpisodeProgress();
        }

        /// <summary>
        /// Sets the episode progress and sends to the player
        /// </summary>
        public void SetEpisodeProgress()
        {
            foreach (PathEpisode pathEpisode in episodes.Values)
                SendServerPathEpisodeProgress(pathEpisode.Id, pathEpisode.ToList());
        }

        /// <summary>
        /// Sets the current episode based on zone and sends to the player
        /// </summary>
        public void SetCurrentZoneEpisode()
        {
            PathEpisodeEntry currentMapEpisode = GetEpisodeForMap();
            if (currentMapEpisode != null && currentMapEpisode.Id != currentEpisode)
            {
                currentEpisode = currentMapEpisode.Id;

                if (!episodes.TryGetValue(currentMapEpisode.Id, out PathEpisode pathEpisode))
                {
                    PathEpisode episode = PathEpisodeCreate(currentMapEpisode.Id);
                    SendServerPathEpisodeProgress(episode.Id, episode);
                }

                SendServerPathCurrentEpisode((ushort)currentMapEpisode.WorldZoneId, (ushort)currentMapEpisode.Id);
            }

            GlobalPathContentManager.Instance.OnEnterZone(player);
        }

        /// <summary>
        /// Get the matching <see cref="PathEpisodeEntry"/> for map the player's currently on
        /// </summary>
        /// <returns></returns>
        private PathEpisodeEntry GetEpisodeForMap()
        {
            // TODO: Use Zone ID & World ID when we can track zone
            uint worldId = player.Map.Entry.Id;
            uint parentZone = GetMostParentZone(player.Zone.Id).Id;
            return GameTableManager.Instance.PathEpisode.Entries.FirstOrDefault(x => x.WorldId == worldId && x.PathTypeEnum == (uint)player.Path && x.WorldZoneId == parentZone);
        }

        private WorldZoneEntry GetMostParentZone(uint zoneId)
        {
            WorldZoneEntry currentZone = GameTableManager.Instance.WorldZone.GetEntry(zoneId);
            if (currentZone == null)
                throw new ArgumentNullException(nameof(zoneId));

            if (currentZone.ParentZoneId == 0)
                return currentZone;

            return GetMostParentZone(currentZone.ParentZoneId);
        }

        private bool GetMission(uint missionId, out PathMission mission)
        {
            mission = null;

            PathMissionEntry pathMissionEntry = GameTableManager.Instance.PathMission.GetEntry(missionId);
            if (pathMissionEntry == null)
                throw new ArgumentOutOfRangeException($"PathMissionEntry not found for ID {missionId}"); // TODO: Use another custom Exception to reflect an entry missing the TBL

            if (episodes.TryGetValue(pathMissionEntry.PathEpisodeId, out PathEpisode pathEpisode))
            {
                mission = pathEpisode.GetMission(missionId);
            }

            return mission != null;
        }

        public void UnlockMission(uint missionId)
        {
            if (missionId == 0)
                throw new ArgumentException("Mission ID must be greater than 0");

            UnlockMissions(new List<uint>
            {
                missionId
            });
        }

        public void UnlockMissions(IEnumerable<uint> missionIds)
        {
            List<PathMission> missionsToSend = new List<PathMission>();

            foreach(uint missionId in missionIds)
            {
                PathMissionEntry pathMissionEntry = GameTableManager.Instance.PathMission.GetEntry(missionId);
                if (pathMissionEntry == null)
                    throw new ArgumentException($"Mission ID {missionId} did not match any PathMissionEntry");
                
                missionsToSend.Add(UnlockMission(pathMissionEntry));
            }

            SendServerPathMissionActivate(missionsToSend.ToArray());
        }

        /// <summary>
        /// Unlocks a <see cref="PathMissionEntry"/> for the Player
        /// </summary>
        /// <param name="pathMissionEntry"></param>
        private PathMission UnlockMission(PathMissionEntry pathMissionEntry)
        {
            if (GetMission(pathMissionEntry.Id, out PathMission matchingMission))
            {
                matchingMission.Unlock();

                return matchingMission;
            }

            return null;
        }

        /// <summary>
        /// Updates all Missions that match <see cref="PathMissionType"/> and Object ID by the given amount.
        /// </summary>
        public void MissionUpdate(PathMissionType missionType, uint objectId, uint amount = 1)
        {
            if (objectId == 0)
                throw new ArgumentOutOfRangeException(nameof(objectId));

            if (!episodes.TryGetValue(currentEpisode, out PathEpisode episode))
                throw new InvalidOperationException("CurrentEpisode must be set and exist in the Episodes dictionary.");

            foreach (PathMission mission in episode.Where(i => i.Type == missionType))
                if (mission.Entry.ObjectId == objectId)
                    mission.UpdateProgress(amount);

            episode.CheckForComplete();
        }

        public void Save(CharacterContext context)
        {
            //log.Debug($"PathMissionManager.Save called");
            foreach (PathEpisode pathEpisode in episodes.Values)
                pathEpisode.Save(context);
        }

        private void SendServerPathEpisodeProgress(uint episodeId, IEnumerable<PathMission> pathMissions)
        {
            List<ServerPathEpisodeInit.Mission> missionProgress = new List<ServerPathEpisodeInit.Mission>();

            foreach(PathMission pathMission in pathMissions)
            {
                if (!pathMission.IsUnlocked())
                    continue;

                missionProgress.Add(new ServerPathEpisodeInit.Mission
                {
                    MissionId = (ushort)pathMission.Id,
                    Completed = pathMission.IsComplete(),
                    Userdata = pathMission.Progress,
                    Statedata = (uint)(pathMission.Progress > 0 && !pathMission.IsComplete() ? 1 : 0)
                });
            }

            player.Session.EnqueueMessageEncrypted(new ServerPathEpisodeInit
            {
                EpisodeId = (ushort)episodeId,
                Missions = missionProgress
            });
        }

        private void SendServerPathCurrentEpisode(ushort zoneId, ushort episodeId)
        {
            player.Session.EnqueueMessageEncrypted(new ServerPathZoneEpisode
            {
                ZoneId = zoneId,
                EpisodeId = episodeId
            });
        }

        private void SendServerPathMissionActivate(IEnumerable<PathMission> pathMissions, byte reason = 1, uint giver = 0)
        {
            List<ServerPathMissionActivate.Mission> missionList = new List<ServerPathMissionActivate.Mission>();

            foreach (PathMission pathMission in pathMissions)
            {
                //log.Debug($"Activating {pathMission.Id}, {pathMission.Completed}, {pathMission.Progress}, {pathMission.State}");
                missionList.Add(new ServerPathMissionActivate.Mission
                {
                    MissionId = (ushort)pathMission.Id,
                    Reason = (byte)(pathMission.IsUnlocked() ? 0 : 1)
                });
            }

            player.Session.EnqueueMessageEncrypted(new ServerPathMissionActivate
            {
                Missions = missionList       
            });
        }

        private void SendServerPathMissionUpdate(PathMission pathMission)
        {
            player.Session.EnqueueMessageEncrypted(new ServerPathMissionUpdate
            {
                MissionId = (ushort)pathMission.Id,
                Completed = pathMission.IsComplete(),
                Userdata = pathMission.Progress
            });
        }
    }
}
