using System.Threading.Tasks;
using NexusForever.WorldServer.Command.Attributes;
using NexusForever.WorldServer.Command.Contexts;
using NexusForever.WorldServer.Game.Entity.Static;
using NLog;

namespace NexusForever.WorldServer.Command.Handler
{
    [Name("RewardTrack")]
    public class RewardTrackCommandHandler : CommandCategory
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public RewardTrackCommandHandler()
            : base(true, "rewardtrack", "rt")
        {
        }

        [SubCommandHandler("addpoints", "rewardTrackId points - Add points to a given Reward Track for this player.")]
        public Task AddPathActivateSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length < 2)
            {
                SendHelpAsync(context);
                return Task.CompletedTask;
            }    

            if (!uint.TryParse(parameters[0], out uint rewardTrackId))
            {
                context.SendErrorAsync($"Unrecognised Reward Track ID. Please try again.");
                return Task.CompletedTask;
            }

            if (!uint.TryParse(parameters[1], out uint points))
            {
                context.SendErrorAsync($"Unable to parse Points value. Please try again.");
                return Task.CompletedTask;
            }

            context.Session.RewardTrackManager.AddPoints(rewardTrackId, points);
            return Task.CompletedTask;
        }
    }
}
