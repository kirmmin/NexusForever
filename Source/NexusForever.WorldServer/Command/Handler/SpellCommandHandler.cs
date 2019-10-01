using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusForever.WorldServer.Command.Attributes;
using NexusForever.WorldServer.Command.Contexts;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Map.Search;
using NexusForever.WorldServer.Game.Spell;

namespace NexusForever.WorldServer.Command.Handler
{
    [Name("Spell")]
    public class SpellCommandHandler : CommandCategory
    {
        public SpellCommandHandler()
            : base(true, "spell")
        {
        }

        [SubCommandHandler("add", "spell4BaseId - Add a spell to the character")]
        public Task AddSpellSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return Task.CompletedTask;

            context.Session.Player.SpellManager.AddSpell(uint.Parse(parameters[0]));
            return Task.CompletedTask;
        }

        [SubCommandHandler("cast", "spell4BaseId [tier] - Cast a spell, optionally supplying the tier")]
        public Task CastSpellSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return Task.CompletedTask;

            byte tier = parameters.Length > 1 ? byte.Parse(parameters[1]) : (byte)1;
            context.Session.Player.CastSpell(uint.Parse(parameters[0]), tier, new SpellParameters
            {
                UserInitiatedSpellCast = false
            });

            return Task.CompletedTask;
        }

        [SubCommandHandler("targetcast", "spell4Id - Make your target cast a spell")]
        public Task TargetCastSpellSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return Task.CompletedTask;

            if (context.Session.Player.TargetGuid == 0u)
                return Task.CompletedTask;

            context.Session.Player.Map.Search(context.Session.Player.Position, context.Session.Player.Map.VisionRange, new SearchCheckRange(context.Session.Player.Position, context.Session.Player.Map.VisionRange), out List<GridEntity> intersectedEntities);

            var targetEntity = intersectedEntities.FirstOrDefault(i => i.Guid == context.Session.Player.TargetGuid);
            if (targetEntity != null)
                if (targetEntity is UnitEntity unit && !(targetEntity is Player))
                    unit.CastSpell(uint.Parse(parameters[0]), new SpellParameters
                    {
                        UserInitiatedSpellCast = false
                    });

            return Task.CompletedTask;
        }

        [SubCommandHandler("resetcooldown", "[spell4Id] - Reset a single spell cooldown, if no spell if supplyed all cooldowns will be reset")]
        public Task ResetCooldownSubCommand(CommandContext context, string command, string[] parameters)
        {
            if (parameters.Length > 0)
                context.Session.Player.SpellManager.SetSpellCooldown(uint.Parse(parameters[0]), 0d);
            else
                context.Session.Player.SpellManager.ResetAllSpellCooldowns();

            return Task.CompletedTask;
        }
    }
}
