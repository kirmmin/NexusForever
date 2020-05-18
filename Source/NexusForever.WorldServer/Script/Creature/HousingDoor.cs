using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Script.Creature
{
    [Script(65852)] // Housing - Decor - Doors - Gothic Gate
    [Script(70052)] // Housing - Decor - Activated - Door
    [Script(75398)] // Redmoon Door (Circular) - Decor - Housing Active Prop
    public class HousingDoor : CreatureScript
    {
        const StandState DOOR_CLOSED = StandState.State0;
        const StandState DOOR_OPEN = StandState.State1;

        public override void OnCreate(WorldEntity me)
        {
            base.OnCreate(me);

            me.StandState = DOOR_CLOSED;
        }

        public override void OnActivate(WorldEntity me, WorldEntity activator)
        {
            base.OnActivate(me, activator);

            // TODO: Add cooldown

            // If Door is Opened, Close.
            if (me.StandState == DOOR_OPEN)
                me.StandState = DOOR_CLOSED;
            else
                me.StandState = DOOR_OPEN;

            // Emit from Player due to way Decor Entities are tracked on the map being... different.
            activator.EnqueueToVisible(new ServerEmote
            {
                Guid = me.Guid,
                StandState = me.StandState
            }, true);
        }
    }
}
