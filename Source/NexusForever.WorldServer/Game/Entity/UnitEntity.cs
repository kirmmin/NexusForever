﻿using System;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract class UnitEntity : WorldEntity
    {
        private readonly List<Spell.Spell> pendingSpells = new List<Spell.Spell>();

        protected UnitEntity(EntityType type)
            : base(type)
        {
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            foreach (Spell.Spell spell in pendingSpells.ToArray())
            {
                spell.Update(lastTick);
                if (spell.IsFinished)
                    pendingSpells.Remove(spell);
            }
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied spell id and <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4Id, SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            Spell4Entry spell4Entry = GameTableManager.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException();

            CastSpell(spell4Entry.Spell4BaseIdBaseSpell, (byte)spell4Entry.TierIndex, parameters);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied spell base id, tier and <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4BaseId, byte tier, SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            SpellBaseInfo spellBaseInfo = GlobalSpellManager.GetSpellBaseInfo(spell4BaseId);
            if (spellBaseInfo == null)
                throw new ArgumentOutOfRangeException();

            SpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
            if (spellInfo == null)
                throw new ArgumentOutOfRangeException();

            parameters.SpellInfo = spellInfo;
            CastSpell(parameters);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            var spell = new Spell.Spell(this, parameters);
            spell.Cast();
            pendingSpells.Add(spell);
        }

        /// <summary>
        /// Cancel any <see cref="Spell"/>'s that are interrupted by movement.
        /// </summary>
        public void CancelSpellsOnMove()
        {
            foreach (Spell.Spell spell in pendingSpells)
                if (spell.IsMovingInterrupted() && spell.IsCasting)
                    spell.CancelCast(CastResult.CasterMovement);
        }

        /// <summary>
        /// Cancel a <see cref="Spell"/> based on its casting id
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        public void CancelSpellCast(uint castingId)
        {
            Spell.Spell spell = pendingSpells.SingleOrDefault(s => s.CastingId == castingId);
            spell?.CancelCast(CastResult.SpellCancelled);
        }

        public void TakeDamage(uint damage)
        {
            int currentShield = (int)GetStatInteger(Stat.Shield);
            int currentHealth = (int)GetStatInteger(Stat.Health);

            while (damage > 0)
            {
                if (currentShield > 0)
                    currentShield--;
                else
                    currentHealth--;

                damage--;

                if (currentHealth == 0)
                    break;
            }

            uint newHealth = (uint)Math.Max(currentHealth - damage, 0);
            SetStat(Stat.Shield, (uint)currentShield);
            SetStat(Stat.Health, (uint)currentHealth);

            if (newHealth > 0)
                EnqueueToVisible(new Server0937
                {
                    UnitId = Guid,
                    Value = newHealth,
                    Unknown0 = false
                }, true);
            else
            {
                EnqueueToVisible(new Server0937
                {
                    UnitId = Guid,
                    Value = newHealth,
                    Unknown0 = false
                }, true);
                EnqueueToVisible(new ServerEntityDeath
                {
                    UnitId = Guid,
                    Dead = true,
                    Reason = 2
                }, true);
            }   
        }
    }
}
