using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity.Movement;
using NexusForever.WorldServer.Game.Map;
using System.Collections.Generic;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class WorldEntity : GridEntity
    {

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            LeashPosition = vector;
            MovementManager = new MovementManager(this, vector, Rotation);
            base.OnAddToMap(map, guid, vector);
        }

        public override void OnRemoveFromMap()
        {
            base.OnRemoveFromMap();
            MovementManager = null;
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> is activated.
        /// </summary>
        public virtual void OnActivate(Player activator)
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> is cast activated.
        /// </summary>
        public virtual void OnActivateCast(Player activator)
        {
            // deliberately empty
        }
    }
}
