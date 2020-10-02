﻿using NexusForever.WorldServer.Game.Entity;

namespace NexusForever.WorldServer.Script
{
    public abstract class CreatureScript : Script
    {
        public virtual void OnCreate(WorldEntity me)
        {
        }

        public virtual void OnAddToMap(WorldEntity me)
        {
        }

        public virtual void OnActivate(WorldEntity me, WorldEntity activator)
        {
        }

        public virtual void OnActivateSuccess(WorldEntity me, WorldEntity activator)
        {
        }

        public virtual void OnActivateFail(WorldEntity me, WorldEntity activator)
        {
        }
    }
}