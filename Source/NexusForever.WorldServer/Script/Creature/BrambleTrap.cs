using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Script.Creature
{
    [Script(27768)] // Bramble Trap
    public class BrambleTrap : CreatureScript
    {
        const StandState STATE_OPEN = StandState.State0;

        public override void OnCreate(WorldEntity me)
        {
            base.OnCreate(me);
        }

        public override void OnActivateSuccess(WorldEntity me, WorldEntity activator)
        {
            base.OnActivateSuccess(me, activator);

            me.StandState = STATE_OPEN;
        }

        public override void OnActivateFail(WorldEntity me, WorldEntity activator)
        {
            base.OnActivateFail(me, activator);

            (activator as UnitEntity).CastSpell(46051, new SpellParameters
            {
                UserInitiatedSpellCast = false,
                IsProxy = true
            });
        }
    }
}
