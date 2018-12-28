using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Achievement.Static;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PathContent.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;

namespace NexusForever.WorldServer.Game.PathContent
{
    public class PathMission : ISaveCharacter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public PathMissionEntry Entry { get; }
        public PathMissionType Type { get; }
        public uint Id { get; }
        public uint Progress { get; private set; }

        public bool IsUnlocked() => unlocked == true;
        private bool unlocked;

        public bool IsComplete() => complete == true;
        private bool complete;

        private Player player;
        private uint maxCount;
        private bool isBooleanCompleted;
        private PathMissionSaveMask saveMask;

        /// <summary>
        /// Create a new <see cref="PathMission"/> from an existing database model.
        /// </summary>
        public PathMission(CharacterPathMissionModel model, Player player)
        {
            this.player = player;
            Id = model.MissionId;
            Entry = GameTableManager.Instance.PathMission.GetEntry(model.MissionId);
            Type = (PathMissionType)Entry.PathMissionTypeEnum;
            Progress = model.Progress;
            unlocked = Convert.ToBoolean(model.Unlocked);
            complete = Convert.ToBoolean(model.Complete);
            SetGoals();

            saveMask = PathMissionSaveMask.None;
        }

        /// <summary>
        /// Create a new <see cref="PathMission"/> from an <see cref="PathMissionEntry"/> template.
        /// </summary>
        public PathMission(Player player, PathMissionEntry entry, bool complete = false, bool unlocked = false, uint progress = 0)
        {
            this.player = player;
            Id = entry.Id;
            Entry = entry;
            Type = (PathMissionType)Entry.PathMissionTypeEnum;
            this.Progress = progress;
            this.unlocked = unlocked;
            this.complete = complete;
            SetGoals();

            saveMask = PathMissionSaveMask.Create;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask != PathMissionSaveMask.None)
            {
                if ((saveMask & PathMissionSaveMask.Create) != 0)
                {
                    // Currency doesn't exist in database, all infomation must be saved
                    context.Add(new CharacterPathMissionModel
                    {
                        Id = player.CharacterId,
                        EpisodeId = Entry.PathEpisodeId,
                        MissionId = Entry.Id,
                        Progress = Progress,
                        Complete = Convert.ToByte(complete),
                        Unlocked = Convert.ToByte(unlocked)
                    });
                }
                else
                {
                    // Currency already exists in database, save only data that has been modified
                    var model = new CharacterPathMissionModel
                    {
                        Id = player.CharacterId,
                        EpisodeId = Entry.PathEpisodeId,
                        MissionId = Entry.Id
                    };

                    // could probably clean this up with reflection, works for the time being
                    EntityEntry<CharacterPathMissionModel> entity = context.Attach(model);
                    if ((saveMask & PathMissionSaveMask.State) != 0)
                    {
                        model.Unlocked = Convert.ToByte(unlocked);
                        entity.Property(p => p.Unlocked).IsModified = true;
                        
                        model.Complete = Convert.ToByte(complete);
                        entity.Property(p => p.Complete).IsModified = true;
                    }

                    if ((saveMask & PathMissionSaveMask.Progress) != 0)
                    {
                        model.Progress = Progress;
                        entity.Property(p => p.Progress).IsModified = true;
                    }
                }
            }

            saveMask = PathMissionSaveMask.None;
        }

        /// <summary>
        /// Update this <see cref="PathMission"/> progress by the given amount
        /// </summary>
        public void UpdateProgress(uint amount)
        {
            if (IsComplete())
                return;

            if (!isBooleanCompleted)
            {
                Progress += Math.Min(amount, maxCount);
                saveMask |= PathMissionSaveMask.Progress;
            }
            else
                complete = true;

            CheckForComplete();
            SendProgressUpdate();

            if (IsComplete())
                Complete();
        }

        /// <summary>
        /// Unlock this <see cref="PathMission"/> for the <see cref="Player"/>, and send them an update.
        /// </summary>
        public void Unlock()
        {
            if (unlocked)
                return;

            unlocked = true;
            saveMask |= PathMissionSaveMask.State;
        }

        private void CheckForComplete()
        {
            if ((!isBooleanCompleted && Progress >= maxCount) || (isBooleanCompleted && complete))
            {
                complete = true;
                saveMask |= PathMissionSaveMask.State;
            }
        }

        private void Complete()
        {
            // TODO: Add in other Achievement Types (like Total Missions for each Path).
            player.AchievementManager.CheckAchievements(player, AchievementType.PathMissionType, Entry.PathMissionTypeEnum);

            // Grant XP
            // TODO: Confirm values at all levels for all tasks. This is close to what was seen in sniffs for low levels, but needs confirmation.
            player.PathManager.AddXp(30);

            // Grant Rewards
            PathRewardEntry episodeReward = GameTableManager.Instance.PathReward.Entries.FirstOrDefault(i => (PathRewardType)i.PathRewardTypeEnum == PathRewardType.Mission && i.ObjectId == Id);
            if (episodeReward == null)
                return;

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

        private void SetGoals()
        {
            switch (Type)
            {
                case PathMissionType.Settler_Hub: // ObjectId == GameTable.PathSettlerHub.Id
                    maxCount = GameTableManager.Instance.PathSettlerHub.GetEntry(Entry.ObjectId).MissionCount;
                    break;
                case PathMissionType.Soldier_Assassinate: // ObjectId == GameTable.PathSoldierAssassinate.Id
                    maxCount = GameTableManager.Instance.PathSoldierAssassinate.GetEntry(Entry.ObjectId).Count;
                    break;
                case PathMissionType.Explorer_Vista: // ObjectId == GameTable.PathExplorerNode.PathExplorerAreaId
                    maxCount = 1;
                    break;
                case PathMissionType.Explorer_ExploreZone: // ObjectId == GameTable.MapZone.Id
                    isBooleanCompleted = true;
                    break;
            }
        }

        private void SendProgressUpdate()
        {
            player.Session.EnqueueMessageEncrypted(new ServerPathMissionUpdate
            {
                MissionId = (ushort)Entry.Id,
                Completed = complete,
                Userdata  = Progress,
                Statedata = (uint)(complete ? 0 : 1)
            });
        }
    }
}
