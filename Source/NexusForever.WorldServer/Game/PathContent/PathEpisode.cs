using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PathContent.Static;
using NLog;

namespace NexusForever.WorldServer.Game.PathContent
{
    public class PathEpisode : ISaveCharacter, IEnumerable<PathMission>
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public PathEpisodeEntry Entry { get; }
        public uint Id { get; }

        public bool IsComplete() => rewardReceived;

        private bool rewardReceived;
        private readonly Player player;
        private Dictionary<uint, PathMission> missions = new Dictionary<uint, PathMission>();
        private readonly uint[] factionTypeEnum = new uint[2];

        private PathEpisodeSaveMask saveMask;

        /// <summary>
        /// Create a new <see cref="PathEpisode"/> from an existing database model.
        /// </summary>
        public PathEpisode(CharacterPathEpisodeModel model, Player player)
        {
            this.player = player;
            Id = model.EpisodeId;
            Entry = GameTableManager.Instance.PathEpisode.GetEntry(model.EpisodeId);
            rewardReceived = Convert.ToBoolean(model.RewardReceived);

            foreach (CharacterPathMissionModel pathMissionModel in model.PathMission)
                missions.Add(pathMissionModel.MissionId, new PathMission(pathMissionModel, player));

            saveMask = PathEpisodeSaveMask.None;
        }

        /// <summary>
        /// Create a new <see cref="PathEpisode"/> from an <see cref="PathEpisodeEntry"/> template.
        /// </summary>
        public PathEpisode(Player player, PathEpisodeEntry entry)
        {
            Entry = entry;
            Id = entry.Id;
            this.player = player;

            if (player.Faction1 == Faction.Exile)
                factionTypeEnum[1] = 1;
            else
                factionTypeEnum[1] = 2;

            foreach (PathMissionEntry pathMissionEntry in GameTableManager.Instance.PathMission.Entries.Where(x => x.PathEpisodeId == Id && factionTypeEnum.Contains(x.PathMissionFactionEnum)))
                missions.Add(pathMissionEntry.Id, new PathMission(player, pathMissionEntry));

            saveMask = PathEpisodeSaveMask.Create;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask != PathEpisodeSaveMask.None)
            {
                if ((saveMask & PathEpisodeSaveMask.Create) != 0)
                {
                    // Currency doesn't exist in database, all infomation must be saved
                    context.Add(new CharacterPathEpisodeModel
                    {
                        Id = player.CharacterId,
                        EpisodeId = Entry.Id,
                        RewardReceived = Convert.ToByte(rewardReceived),
                    });
                }
                else
                {
                    var model = new CharacterPathEpisodeModel
                    {
                        Id = player.CharacterId,
                        EpisodeId = Entry.Id
                    };
                }
            }

            foreach (PathMission pathMission in missions.Values)
                pathMission.Save(context);

            saveMask = PathEpisodeSaveMask.None;
        }

        /// <summary>
        /// Returns a matching <see cref="PathMission"/> if it exists within this episode
        /// </summary>
        public PathMission GetMission(uint missionId)
        {
            return missions.TryGetValue(missionId, out PathMission pathMission) ? pathMission : null;
        }

        /// <summary>
        /// The <see cref="PathEpisode"/> will check for all missions complete and reward if necessary.
        /// </summary>
        public void CheckForComplete()
        {
            if (rewardReceived)
                return;

            if (missions.Values.Where(i => !i.IsComplete()).Count() > 0)
                return;

            rewardReceived = true;
            saveMask |= PathEpisodeSaveMask.RewardChange;

            PathRewardEntry episodeReward = GameTableManager.Instance.PathReward.Entries.FirstOrDefault(i => (PathRewardType)i.PathRewardTypeEnum == PathRewardType.Episode && i.ObjectId == Id);
            if (episodeReward == null)
                throw new InvalidOperationException($"PathRewardEntry not found for Episode ID {Id}: {nameof(episodeReward)}");

            if (episodeReward.Item2Id > 0)
                player.Inventory.ItemCreate(episodeReward.Item2Id, 1, ItemUpdateReason.PathReward);

            if (episodeReward.Spell4Id > 0)
            {
                Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(episodeReward.Spell4Id);
                player.SpellManager.AddSpell(spell4Entry.Spell4BaseIdBaseSpell, (byte)spell4Entry.TierIndex);
            }

            if (episodeReward.CharacterTitleId > 0)
                player.TitleManager.AddTitle((ushort)episodeReward.CharacterTitleId);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<PathMission> GetEnumerator()
        {
            return missions.Values.GetEnumerator();
        }
    }
}
