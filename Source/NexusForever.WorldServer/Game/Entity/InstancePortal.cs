using NexusForever.Database.World.Model;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Script;

namespace NexusForever.WorldServer.Game.Entity
{
    [DatabaseEntity(EntityType.InstancePortal)]
    public class InstancePortal : WorldEntity
    {
        public InstancePortal()
            : base(EntityType.InstancePortal)
        {
        }

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);
            ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnCreate(this);
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new InstancePortalEntityModel
            {
                CreatureId = CreatureId,
                RemainingTimeMs = 0 // None of these appear to actually be timed
            };
        }

        public override ServerEntityCreate BuildCreatePacket()
        {
            ServerEntityCreate entityCreate = base.BuildCreatePacket();
            entityCreate.CreateFlags = 0;

            return entityCreate;
        }

        public override void OnInteract(Player activator)
        {
            ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnActivate(this, activator);
        }

        public override void OnActivateSuccess(Player activator)
        {
            ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnActivateSuccess(this, activator);
        }
    }
}
