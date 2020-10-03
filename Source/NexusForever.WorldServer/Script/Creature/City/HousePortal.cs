using NexusForever.Shared.Game.Events;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Housing;

namespace NexusForever.WorldServer.Script.Creature.City
{
    [Script(26350)]
    public class HousePortal : CreatureScript
    {
        readonly uint[] CONST_SPELL_TRAINING = {
            22919, // Recall - House
            25520  // Escape House
        };

        public override void OnActivateSuccess(WorldEntity me, WorldEntity activator)
        {
            base.OnActivateSuccess(me, activator);

            if (!(activator is Player player))
                return;
            
            player.Session.EnqueueEvent(new TaskGenericEvent<Residence>(ResidenceManager.Instance.GetResidence(player.Name),
                residence =>
            {
                if (residence == null)
                    residence = ResidenceManager.Instance.CreateResidence(player);

                foreach (uint spellBaseId in CONST_SPELL_TRAINING)
                    if (player.SpellManager.GetSpell(spellBaseId) == null)
                        player.SpellManager.AddSpell(spellBaseId);

                ResidenceEntrance entrance = ResidenceManager.Instance.GetResidenceEntrance(residence);
                player.TeleportTo(entrance.Entry, entrance.Position, 0u, residence.Id);
            }));
        }
    }
}
