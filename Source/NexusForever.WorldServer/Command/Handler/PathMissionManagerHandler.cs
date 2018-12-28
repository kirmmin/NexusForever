using System.Collections.Generic;
using System.Threading.Tasks;
using NexusForever.WorldServer.Command.Attributes;
using NexusForever.WorldServer.Command.Contexts;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PathContent;
using NexusForever.WorldServer.Game.PathContent.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;

namespace NexusForever.WorldServer.Command.Handler
{
    [Name("Path")]
    public class PathMissionManagerCommandHandler : CommandCategory
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public PathMissionManagerCommandHandler()
            : base(true, "pm", "pathmission")
        {
        }

        [SubCommandHandler("episodeadd", "episodeId - Creates episode for player")]
        public Task AddPathMissionManagerEpisodeSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length <= 0)
            {
                context.SendErrorAsync($"Parameter missing.");
                return Task.CompletedTask;
            }

            uint episodeId = 0;
            if (parameters.Length > 0)
                episodeId = uint.Parse(parameters[0]);

            if(episodeId > 0)
            {
                PathEpisode pathEpisode = context.Session.Player.PathMissionManager.PathEpisodeCreate(episodeId);
                context.SendMessageAsync($"Executing PathEpisodeCreate with episode {pathEpisode.Id}");
            }
            else
            {
                context.SendErrorAsync($"Unknown episode: {episodeId}");
            }

            return Task.CompletedTask;
        }

        [SubCommandHandler("missionunlock", "missionId - Unlocks mission for player")]
        public Task AddPathMissionManagerMissionUnlockSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length <= 0)
            {
                context.SendErrorAsync($"Parameter mising.");
                return Task.CompletedTask;
            }

            uint missionId = 0;
            if (parameters.Length > 0)
                missionId = uint.Parse(parameters[0]);

            if (missionId > 0)
                context.Session.Player.PathMissionManager.UnlockMission(missionId);
            else
                context.SendErrorAsync($"Unknown episode: {missionId}");

            return Task.CompletedTask;
        }

        //[SubCommandHandler("missionupdate", "missionId progress state - Unlocks mission for player")]
        //public Task AddPathMissionManagerMissionUpdateSubCommand(CommandContext context, string command, string[] parameters)
        //{
        //    if (parameters.Length <= 2)
        //    {
        //        context.SendErrorAsync($"Parameter mising.");
        //        return Task.CompletedTask;
        //    }

        //    uint missionId = 0;
        //    if (parameters.Length > 0)
        //        missionId = uint.Parse(parameters[0]);

        //    uint progress = 0;
        //    if (parameters.Length > 1)
        //        progress = uint.Parse(parameters[1]);

        //    uint state = 0;
        //    if (parameters.Length > 2)
        //        state = uint.Parse(parameters[2]);

        //    if (missionId > 0)
        //    {
        //        context.Session.Player.PathMissionManager.UpdateMission(missionId, progress, (MissionState)state);
        //    }
        //    else
        //    {
        //        context.SendErrorAsync($"Unknown episode: {missionId}");
        //    }

        //    return Task.CompletedTask;
        //}

        //[SubCommandHandler("missiontest", "missionId progress state - Unlocks mission for player")]
        //public Task AddPathMissionManagerMissionTestSubCommand(CommandContext context, string command, string[] parameters)
        //{
        //    if (parameters.Length <= 2)
        //    {
        //        context.SendErrorAsync($"Parameter mising.");
        //        return Task.CompletedTask;
        //    }

        //    uint missionId = 0;
        //    if (parameters.Length > 0)
        //        missionId = uint.Parse(parameters[0]);

        //    uint progress = 0;
        //    if (parameters.Length > 1)
        //        progress = uint.Parse(parameters[1]);

        //    uint state = 0;
        //    if (parameters.Length > 2)
        //        state = uint.Parse(parameters[2]);

        //    uint reason = 1;
        //    if (parameters.Length > 3)
        //        reason = uint.Parse(parameters[3]);

        //    uint giver = 0;
        //    if (parameters.Length > 4)
        //        giver = uint.Parse(parameters[4]);

        //    if (missionId > 0)
        //    {
        //        context.Session.EnqueueMessageEncrypted(new ServerPathMissionActivate
        //        {
        //            Missions = new List<ServerPathMissionActivate.Mission>
        //            {
        //                new ServerPathMissionActivate.Mission
        //                {
        //                    MissionId = missionId,
        //                    Completed = false,
        //                    Userdata = progress,
        //                    Statedata = (MissionState)state,
        //                    Reason = (byte)reason,
        //                    Giver = giver
        //                }
        //            }
        //        });
        //    }
        //    else
        //    {
        //        context.SendErrorAsync($"Unknown episode: {missionId}");
        //    }

        //    return Task.CompletedTask;
        //}
    }
}
