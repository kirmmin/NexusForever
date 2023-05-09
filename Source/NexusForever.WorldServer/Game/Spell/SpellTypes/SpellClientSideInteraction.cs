using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Spell.Event;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;
using System.Linq;

namespace NexusForever.WorldServer.Game.Spell
{
    [SpellType(CastMethod.ClientSideInteraction)]
    public partial class SpellClientSideInteraction : Spell, ISpell
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static CastMethod castMethod = CastMethod.ClientSideInteraction;

        public SpellClientSideInteraction(UnitEntity caster, SpellParameters parameters) 
            : base(caster, parameters, castMethod)
        {
        }

        public override bool Cast()
        {
            if (!base.Cast())
                return false;

            double castTime = parameters.CastTimeOverride > 0 ? parameters.CastTimeOverride / 1000d : parameters.SpellInfo.Entry.CastTime / 1000d;
            if ((CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod != CastMethod.ClientSideInteraction)
                events.EnqueueEvent(new SpellEvent(castTime, SucceedClientInteraction));

            status = SpellStatus.Casting;
            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started casting.");
            return true;
        }

        protected override bool _IsCasting()
        {
            return base._IsCasting() && status == SpellStatus.Casting;
        }

        /// <summary>
        /// Used when a <see cref="CSI.ClientSideInteraction"/> succeeds
        /// </summary>
        /// <remarks>
        /// Some spells offer a CSI "Event" in the client - a dialog box, a mini-game, etc. - but, do not have a ClientUniqueId as not triggered by player directly doing something.
        /// In this case they are spells cast by something else that require player interaction, e.g. when you get rooted but can break the root by holding down a key.
        /// We only generated a <see cref="CSI.ClientSideInteraction"/> instance in the cases where the client delivers a ClientUniqueId.
        /// </remarks>
        public void SucceedClientInteraction()
        {
            Execute();

            if (parameters.SpellInfo.Effects.FirstOrDefault(x => (SpellEffectType)x.EffectType == SpellEffectType.Activate) == null)
                parameters.ClientSideInteraction?.TriggerSuccess();
        }

        /// <summary>
        /// Used when a <see cref="CSI.ClientSideInteraction"/> fails
        /// </summary>
        public void FailClientInteraction()
        {
            parameters.ClientSideInteraction?.TriggerFail();

            CancelCast(CastResult.ClientSideInteractionFail);
        }

        protected override uint GetPrimaryTargetId()
        {
            return parameters.ClientSideInteraction?.Entry != null ? caster.Guid : parameters.PrimaryTargetId;
        }
    }
}
