using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace NexusForever.WorldServer.Game.AI
{
    public class UnitAI : IUpdate
    {
        protected UnitEntity me;

        private Vector3 originalLocation = Vector3.Zero;
        private Vector3 originalRotation = Vector3.Zero;
        private bool resettingFromCombat = false;

        public bool IsLeashing() => resettingFromCombat;

        private float MAX_ATTACK_RANGE = 5f;
        private uint[] autoAttacks = new uint[]
        {
            28704,
            28705
        };
        private bool attackIndex;
        private UpdateTimer autoTimer = new UpdateTimer(1.5d);

        public UnitAI(UnitEntity unit)
        {
            me = unit;
        }

        public virtual void Update(double lastTick)
        {
            autoTimer.Update(lastTick);

            if (me.InCombat)
            {
                if (resettingFromCombat)
                    return;

                if (!me.GetCurrentTarget(out UnitEntity victim))
                {
                    resettingFromCombat = true;
                    return;
                }

                if (WillLeash())
                    return;

                me.MovementManager?.Chase(victim, me.GetPropertyValue(Property.MoveSpeedMultiplier) * 7f, MAX_ATTACK_RANGE);

                DoAutoAttack();
            }

            if (resettingFromCombat)
            {
                me.MovementManager?.MoveTo(originalLocation, me.GetPropertyValue(Property.MoveSpeedMultiplier) * 10f * 1.5f);
                if (me.Position.GetDistance(originalLocation) < 1f)
                    Reset();
            }
        }

        private bool WillLeash()
        {
            if (originalLocation.GetDistance(me.Position) > 80f)
            {
                ExitCombat();
                return true;
            }

            return false;
        }

        private void Reset()
        {
            originalLocation = Vector3.Zero;
            me.MovementManager?.SetRotation(originalRotation);
            me.Health = me.MaxHealth;
            resettingFromCombat = false;
        }

        public void ExitCombat()
        {
            resettingFromCombat = true;
            me.ThreatManager.ClearThreatList();
        }

        public virtual void OnEnterCombat()
        {
            originalLocation = me.Position;
        }

        public virtual void OnExitCombat()
        {
            ExitCombat();
        }

        protected void DoAutoAttack()
        {
            if (me.IsCasting() || !me.InCombat || !me.IsAlive)
                return;

            if (!me.GetCurrentTarget(out UnitEntity victim))
                return;

            uint spell4Id = autoAttacks[Convert.ToInt32(attackIndex)];
            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException("spell4Id", $"{spell4Id} not found in Spell4 Entries.");

            if (me.Position.GetDistance(victim.Position) > spell4Entry.TargetMaxRange)
                return;

            if (!autoTimer.HasElapsed)
                return;

            me.CastSpell(spell4Id, new SpellParameters
            {
                PrimaryTargetId = victim.Guid
            });
            attackIndex = !attackIndex;
            autoTimer.Reset(true);
        }
    }
}
