using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell.Static;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Combat
{
    public static class DamageCalculator
    {
        public static uint GetbaseDamageForSpell(WorldEntity attacker, float parameterType, float parameterValue)
        {
            switch (parameterType)
            {
                case 10:
                    return (uint)Math.Round(attacker.Level * parameterValue);
                case 12:
                    return (uint)Math.Round(attacker.GetAssaultPower() * parameterValue);
                case 13:
                    return (uint)Math.Round(attacker.GetSupportPower() * parameterValue);
            }

            return 0u;
        }

        public static uint GetBaseDamage(uint damage)
        {
            return (uint)(damage * (new Random().Next(95, 103) / 100f));
        }

        public static uint GetDamageAfterArmorMitigation(WorldEntity victim, DamageType damageType, uint damage, uint attackerLevel)
        {
            GameFormulaEntry armorFormulaEntry = GameTableManager.Instance.GameFormula.GetEntry(1234);
            float maximumArmorMitigation = (float)(armorFormulaEntry.Dataint01 / 100f);
            float mitigationPct = (armorFormulaEntry.Datafloat0 / attackerLevel * armorFormulaEntry.Datafloat01) * victim.GetPropertyValue(Property.Armor) / 100f;

            if (damageType == DamageType.Physical)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetMagic);
            else if (damageType == DamageType.Tech)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetTech);
            else if (damageType == DamageType.Magic)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetMagic);

            if (mitigationPct > 0f)
                damage = (uint)(damage * (1f - Math.Clamp(mitigationPct, 0f, maximumArmorMitigation)));

            return damage;
        }

        public static (uint, bool) GetCrit(uint damage, float critRate)
        {
            if (critRate > 1f)
                return (damage, false);

            float baseCritSeverity = 1.5f;

            bool crit = false;
            crit = new Random().Next(1, 100) <= critRate * 100f;
            if (crit)
                damage = (uint)(damage * baseCritSeverity);

            return (damage, crit);
        }

        public static uint GetShieldAmount(uint damage, WorldEntity victim)
        {
            uint maxShieldAmount = (uint)(damage * 0.625f); //GetPropertyValue(Property.ShieldMitigationMax).Value);
            uint shieldedAmount = maxShieldAmount >= victim.Shield ? victim.Shield: maxShieldAmount;

            return shieldedAmount;
        }
    }
}
