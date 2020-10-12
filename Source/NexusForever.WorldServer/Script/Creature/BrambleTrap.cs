using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Script.Creature
{
    [Script(27768)] // Bramble Trap
    public class BrambleTrap : CreatureScript
    {
        const uint SPELL_PENALTY = 46051;

        public override void OnCreate(WorldEntity me)
        {
            base.OnCreate(me);
        }

        public override void OnActivateSuccess(WorldEntity me, WorldEntity activator)
        {
            base.OnActivateSuccess(me, activator);

            (activator as UnitEntity).GetActiveSpell(s => s.Spell4Id == SPELL_PENALTY)?.Finish();
            me.EnqueueToVisible(new ServerEntityDeath
            {
                UnitId = me.Guid,
                Dead = true,
                Reason = 2
            });
        }

        public override void OnActivateFail(WorldEntity me, WorldEntity activator)
        {
            base.OnActivateFail(me, activator);

            (activator as UnitEntity).CastSpell(SPELL_PENALTY, new SpellParameters
            {
                PrimaryTargetId = activator.Guid,
                UserInitiatedSpellCast = false,
                IsProxy = true
            });
        }
    }
}
