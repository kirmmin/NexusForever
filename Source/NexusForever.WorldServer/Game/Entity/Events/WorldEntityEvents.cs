using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.CSI;
using NexusForever.WorldServer.Game.Entity.Movement;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.Prerequisite;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Script;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract partial class WorldEntity : GridEntity
    {

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            LeashPosition = vector;
            LeashRotation = Rotation;
            MovementManager = new MovementManager(this, vector, Rotation);
            base.OnAddToMap(map, guid, vector);

            if (Type != EntityType.Plug && Type != EntityType.Player)
                ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnAddToMap(this);
        }

        public override void OnRemoveFromMap()
        {
            base.OnRemoveFromMap();
            MovementManager = null;
        }

        // TODO: research the difference between a standard activation and cast activation

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> is activated.
        /// </summary>
        public virtual void OnInteract(Player activator)
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> is cast activated.
        /// </summary>
        public virtual void OnActivateCast(Player activator, uint interactionId)
        {
            Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);

            uint spell4Id = 0;
            for (int i = 0; i < entry.Spell4IdActivate.Length; i++)
            {
                if (spell4Id > 0u || i == entry.Spell4IdActivate.Length)
                    break;

                if (entry.PrerequisiteIdActivateSpells[i] > 0 && PrerequisiteManager.Instance.Meets(activator, entry.PrerequisiteIdActivateSpells[i]))
                    spell4Id = entry.Spell4IdActivate[i];

                if (spell4Id == 0u && entry.Spell4IdActivate[i] == 0u && i > 0)
                    spell4Id = entry.Spell4IdActivate[i - 1];
            }

            if (spell4Id == 0)
                throw new InvalidOperationException($"Spell4Id should not be 0. Unhandled Creature ActivateCast {CreatureId}");

            SpellParameters parameters = new SpellParameters
            {
                PrimaryTargetId = Guid,
                ClientSideInteraction = new ClientSideInteraction(activator, this, interactionId),
                CastTimeOverride = (int)entry.ActivateSpellCastTime,
            };
            activator.CastSpell(spell4Id, parameters);
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/>'s activate succeeds.
        /// </summary>
        public virtual void OnActivateSuccess(Player activator)
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/>'s activation fails.
        /// </summary>
        public virtual void OnActivateFail(Player activator)
        {
            // deliberately empty
            ScriptManager.Instance.GetScript<CreatureScript>(CreatureId)?.OnActivateFail(this, activator);
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> enters the range of this <see cref="WorldEntity"/>.
        /// </summary>
        public virtual void OnEnterRange(WorldEntity entity)
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when <see cref="WorldEntity"/> leaves the range of this <see cref="WorldEntity"/>.
        /// </summary>
        public virtual void OnExitRange(WorldEntity entity)
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when a <see cref="Stat"/> value is updated
        /// </summary>
        protected virtual void OnStatChange(Stat stat, float newVal, float previousVal)
        {
            // deliberately empty
        }
    }
}
