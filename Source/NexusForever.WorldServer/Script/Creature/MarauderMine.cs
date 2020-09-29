using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Script.Creature
{
    [Script(16718)] // Housing - Decor - Doors - Gothic Gate
    public class MarauderMine : CreatureScript
    {
        
        public override void OnCreate(WorldEntity me)
        {
            base.OnCreate(me);
        }

        public override void OnActivate(WorldEntity me, WorldEntity activator)
        {
            base.OnActivate(me, activator);

            (me as UnitEntity).CastSpell(26443, new Game.Spell.SpellParameters());
        }
    }
}
