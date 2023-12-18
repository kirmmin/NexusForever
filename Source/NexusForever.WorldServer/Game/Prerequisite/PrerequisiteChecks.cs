using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexusForever.Shared;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Prerequisite.Static;
using NexusForever.WorldServer.Game.Quest.Static;
using NexusForever.WorldServer.Game.Reputation.Static;
using System;

namespace NexusForever.WorldServer.Game.Prerequisite
{
    public sealed partial class PrerequisiteManager
    {
        [PrerequisiteCheck(PrerequisiteType.Level)]
        private static bool PrerequisiteCheckLevel(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Level == value;
                case PrerequisiteComparison.NotEqual:
                    return player.Level != value;
                case PrerequisiteComparison.GreaterThan:
                    return player.Level > value;
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return player.Level >= value;
                case PrerequisiteComparison.LessThan:
                    return player.Level < value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return player.Level <= value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Level}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Race)]
        private static bool PrerequisiteCheckRace(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Race == (Race)value;
                case PrerequisiteComparison.NotEqual:
                    return player.Race != (Race)value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Race}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Class)]
        private static bool PrerequisiteCheckClass(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Class == (Class)value;
                case PrerequisiteComparison.NotEqual:
                    return player.Class != (Class)value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Class}!");
                    return true;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Quest)]
        private static bool PrerequisiteCheckQuest(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.QuestManager.GetQuestState((ushort)objectId) == (QuestState)value;
                case PrerequisiteComparison.NotEqual:
                    return player.QuestManager.GetQuestState((ushort)objectId) != (QuestState)value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Quest}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Path)]
        private static bool PrerequisiteCheckPath(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.PathManager.IsPathActive((Path)value);
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Path}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Achievement)]
        private static bool PrerequisiteCheckAchievement(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    if (value <= 1)
                        return player.AchievementManager.HasCompletedAchievement((ushort)objectId) != Convert.ToBoolean(value);
                    else
                    {
                        log.Warn($"Unhandled PrerequisiteComparison {comparison} with value {value} for {PrerequisiteType.Achievement}!");
                        return false;
                    }
                case PrerequisiteComparison.Equal:
                    if (value <= 1)
                        return player.AchievementManager.HasCompletedAchievement((ushort)objectId) == Convert.ToBoolean(value);
                    else
                    {
                        log.Warn($"Unhandled PrerequisiteComparison {comparison} with value {value} for {PrerequisiteType.Achievement}!");
                        return false;
                    }
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Achievement}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.SpellBaseId)]
        private static bool PrerequisiteCheckSpellBaseId(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    return player.SpellManager.GetSpell(objectId) == null;
                case PrerequisiteComparison.Equal:
                    return player.SpellManager.GetSpell(objectId) != null;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Achievement}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Spell)]
        private static bool PrerequisiteCheckSpell(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            if (value == 0 && objectId == 0)
                return false;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.HasSpell(s => s.Spell4Id == value, out Spell.Spell equalSpell);
                case PrerequisiteComparison.NotEqual:
                    return !player.HasSpell(s => s.Spell4Id == value, out Spell.Spell notEqualSpell);
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Spell}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.InCombat)]
        private static bool PrerequisiteCheckInCombat(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.InCombat == true; // TODO: Return true if Player is in Combat
                case PrerequisiteComparison.NotEqual:
                    return player.InCombat != true; // TODO: Return ture if Player is NOT in Combat.
                default:
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.HasBuff)]
        private static bool PrerequisiteCheckHasBuff(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            if (value == 0 && objectId == 0)
                return false;

            Spell4Entry spell4 = GameTableManager.Instance.Spell4.GetEntry(value);
            if (spell4 == null)
                throw new InvalidOperationException();

            Spell.Spell buff = null;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.HasSpell(value, out buff);
                case PrerequisiteComparison.NotEqual:
                    return !player.HasSpell(value, out buff);
                default:
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.WorldReq)]
        private static bool PrerequisiteCheckWorldReq(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Figure out what the objectId is. Is it a specific world? A world state? Player state?
            // Needs research.

            // Noticed in a RavelSignal spell effect. Only reference to Ravel was found here http://www.jtrebas.com/game/wildstar/. It appears to be in reference to a script. The word "ravel" threw me off a little, but googling and seeing how it's described as a noun here (https://www.dictionary.com/browse/ravel) - "a tangle or complication" - suggests that it's a way of adjusting things on the fly from a script. I wonder if the WorldReq was just a check to see if a script evaluated to true/false - Kirmmin (April 5, 2020)

            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Faction2)]
        private static bool PrerequisiteCheckFaction2(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Faction1 == (Faction)value;
                case PrerequisiteComparison.NotEqual:
                    return player.Faction1 != (Faction)value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Faction2}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.AccountItemClaimed)]
        private static bool PrerequisiteCheckAccountItemClaimed(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    return !player.Inventory.HasItem(value);
                case PrerequisiteComparison.Equal:
                    return player.Inventory.HasItem(value);
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.AccountItemClaimed}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.BaseFaction)]
        private static bool PrerequisiteCheckBaseFaction(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Faction1 == (Faction)value;
                case PrerequisiteComparison.NotEqual:
                    return player.Faction1 != (Faction)value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.BaseFaction}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.CosmicRewards)]
        private static bool PrerequisiteCheckCosmicRewards(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    return player.Session.AccountCurrencyManager.GetAmount(Account.Static.AccountCurrencyType.CosmicReward) != value;
                case PrerequisiteComparison.Equal:
                    return player.Session.AccountCurrencyManager.GetAmount(Account.Static.AccountCurrencyType.CosmicReward) == value;
                case PrerequisiteComparison.GreaterThan:
                case PrerequisiteComparison.LessThanOrEqual: 
                    // The conditional below is intentionally "incorrect". It's possible PrerequisiteComparison 4 is actually GreaterThanOrEqual
                    return player.Session.AccountCurrencyManager.GetAmount(Account.Static.AccountCurrencyType.CosmicReward) >= value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.CosmicRewards}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.HoverboardFlair)]
        private static bool PrerequestCheckHoverboardFlair(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.PetCustomisationManager.GetCustomisation(PetType.HoverBoard, objectId) != null;
                case PrerequisiteComparison.NotEqual:
                    return player.PetCustomisationManager.GetCustomisation(PetType.HoverBoard, objectId) == null;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.HoverboardFlair}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Plane)]
        private static bool PrerequisiteCheckPlane(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // Unknown how this works at this time, but there is a Spell Effect called "ChangePlane". Could be related.
            // TODO: Investigate further.

            // Returning true by default as many mounts used this
            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Prerequisite)]
        private static bool PrerequisiteCheckPrerequisite(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    return !Instance.Meets(player, objectId);
                case PrerequisiteComparison.Equal:
                    return Instance.Meets(player, objectId);
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.Prerequisite}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Stealth)]
        private static bool PrerequisiteCheckStealth(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Add value to the check. It's a spell4 Id.

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Stealthed == true; // TODO: Add OR check for Spell4 Effect
                case PrerequisiteComparison.NotEqual:
                    return player.Stealthed == false; // TODO: Add AND check for Spell4 Effect
                default:
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.SpellObj)]
        private static bool PrerequisiteCheckSpellObj(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Confirm how the objectId is calculated. It seems like this check always checks for a Spell that is determined by an objectId.

            // Error message is "Spell requirement not met"

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.SpellManager.GetSpell(value) != null;
                case PrerequisiteComparison.NotEqual:
                    return player.SpellManager.GetSpell(value) == null;
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.SpellObj}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.TargetIsPlayer)]
        private static bool PrerequisiteCheckTargetIsPlayer(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Currently this is a wasted effort. We only evaluate prereq's against Players. This suggests we may need to start evaluating against all entities.

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player is Player;
                case PrerequisiteComparison.NotEqual:
                    return !(player is Player);
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.TargetIsPlayer}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Unhealthy)]
        private static bool PrerequesiteCheckUnhealthy(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Investigate further. Unknown what the value and objectId refers to at this time.

            // Error message is "Cannot recall while in Unhealthy Time" when trying to use Rapid Transport & other recall spells
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    return !player.InCombat;
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.Unhealthy}!");
                    return true;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Unknown194)]
        private static bool PrerequisiteCheckUnknown194(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Only used in Mount check prerequisites. Its use is unknown.

            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Unknown195)]
        private static bool PrerequisiteCheckUnknown195(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            // TODO: Only used in Mount check prerequisites. Its use is unknown.

            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Vital)]
        private static bool PrerequisiteCheckVital(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.GetVitalValue((Vital)objectId) == value;
                case PrerequisiteComparison.NotEqual:
                    return player.GetVitalValue((Vital)objectId) != value;
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return player.GetVitalValue((Vital)objectId) >= value;
                case PrerequisiteComparison.GreaterThan:
                    return player.GetVitalValue((Vital)objectId) > value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return player.GetVitalValue((Vital)objectId) <= value;
                case PrerequisiteComparison.LessThan:
                    return player.GetVitalValue((Vital)objectId) < value;
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.Vital}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.VitalPercent)]
        private static bool PrerequisiteCheckVitalPercent(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            float max = 0;
            switch (objectId)
            {
                case 1:
                    max = player.GetPropertyValue(Property.BaseHealth);
                    break;
                case 3:
                    max = player.GetPropertyValue(Property.ShieldCapacityMax);
                    break;
                case 5:
                    max = player.GetPropertyValue(Property.ResourceMax0);
                    break;
                case 6:
                    max = player.GetPropertyValue(Property.ResourceMax1);
                    break;
                case 8:
                    max = player.GetPropertyValue(Property.ResourceMax3);
                    break;
                case 15:
                    max = player.GetPropertyValue(Property.BaseFocusPool);
                    break;
                default:
                    log.Warn($"Unhandled objectId: {objectId} for {PrerequisiteType.SpellMechanic}");
                    break;
            }

            float percentage = player.GetVitalValue((Vital)objectId) / max * 100;

            switch (comparison)
            {
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return percentage >= value;
                case PrerequisiteComparison.GreaterThan:
                    return percentage > value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return percentage <= value;
                case PrerequisiteComparison.LessThan:
                    return percentage < value;
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.Vital}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.ActiveSpellCount)]
        private static bool PrerequisiteCheckSpellActiveSpellCount(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.GetActiveSpellCount(s => s.Spell4Id == objectId && !s.IsFinished) == value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return player.GetActiveSpellCount(s => s.Spell4Id == objectId && !s.IsFinished) <= value;
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.ActiveSpellCount}");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.SpellMechanic)]
        private static bool PrerequisiteCheckSpellMechanic(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            uint resource = 0;
            switch (objectId)
            {
                case 4:
                    resource = (uint)player.GetVitalValue(Vital.SpellSurge);
                    break;
                default:
                    log.Warn($"Unhandled objectId: {objectId} for {PrerequisiteType.SpellMechanic}");
                    break;
            }

            switch (comparison)
            {
                case PrerequisiteComparison.GreaterThan:
                    return resource > value;
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.SpellMechanic}");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.PositionalRequirement)]
        private static bool PrerequisiteCheckPositionalRequirement(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            if (target == null || objectId == 0)
                return false;

            PositionalRequirementEntry entry = GameTableManager.Instance.PositionalRequirement.GetEntry(objectId);

            float angle = (target.Position.GetRotationTo(player.Position) - target.Rotation).X.ToDegrees();
            float minBounds = entry.AngleCenter - entry.AngleRange / 2f;
            float maxBounds = entry.AngleCenter + entry.AngleRange / 2f;
            bool isAllowed = angle >= minBounds && angle <= maxBounds;
                 
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return isAllowed;
                case PrerequisiteComparison.NotEqual:
                    return !isAllowed;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PositionalRequirement}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Entitlement)]
        private static bool PrerequisiteCheckEntitlement(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            EntitlementEntry entry = GameTableManager.Instance.Entitlement.GetEntry(objectId);
            if (entry == null)
                throw new ArgumentException($"Invalid entitlement type {objectId}!");

            uint currentValue = 0;

            if (((EntitlementFlags)entry.Flags).HasFlag(EntitlementFlags.Character))
                currentValue = player.Session.EntitlementManager.GetCharacterEntitlement((EntitlementType)objectId)?.Amount ?? 0u;
            else
                currentValue = player.Session.EntitlementManager.GetAccountEntitlement((EntitlementType)objectId)?.Amount ?? 0u;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return currentValue == value;
                case PrerequisiteComparison.NotEqual:
                    return currentValue != value;
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return currentValue >= value;
                case PrerequisiteComparison.GreaterThan:
                    return currentValue > value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return currentValue <= value;
                case PrerequisiteComparison.LessThan:
                    return currentValue < value;
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PositionalRequirement}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.CostumeUnlocked)]
        private static bool PrerequisiteCheckCostumeUnlocked(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.CostumeManager.HasCostumeItemUnlocked(objectId);
                case PrerequisiteComparison.NotEqual:
                    return !player.CostumeManager.HasCostumeItemUnlocked(objectId);
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PositionalRequirement}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Amp)]
        private static bool PrerequisiteCheckAmp(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.SpellManager.IsAmpEnabled((ushort)objectId);
                default:
                    log.Warn($"Unhandled {comparison} for {PrerequisiteType.Vital}!");
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.PurchasedTitle)]
        private static bool PrerequisiteCheckPurchasedTitle(Player player, PrerequisiteComparison comparison, uint value, uint objectId, UnitEntity target)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.TitleManager.HasTitle((ushort)objectId);
                case PrerequisiteComparison.NotEqual:
                    return !player.TitleManager.HasTitle((ushort)objectId);
                default:
                    log.Warn($"Unhandled PrerequisiteComparison {comparison} for {(PrerequisiteType)288}!");
                    return true;
            }
        }
    }
}
