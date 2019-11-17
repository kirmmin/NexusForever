using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Prerequisite.Static;

namespace NexusForever.WorldServer.Game.Prerequisite
{
    public sealed partial class PrerequisiteManager
    {
        [PrerequisiteCheck(PrerequisiteType.Level)]
        private static bool PrerequisiteCheckLevel(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            // 24
            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Race)]
        private static bool PrerequisiteCheckRace(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Race == (Race)value;
                case PrerequisiteComparison.NotEqual:
                    return player.Race != (Race)value;
                default:
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Class)]
        private static bool PrerequisiteCheckClass(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            // 44
            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.SpellKnown)]
        private static bool PrerequisiteCheckSpellKnown(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.SpellManager.GetSpell(value) != null;
                case PrerequisiteComparison.NotEqual:
                    return player.SpellManager.GetSpell(value) == null;
                default:
                    return false;
            }
        }

        [PrerequisiteCheck(PrerequisiteType.Plane)]
        private static bool PrerequisiteCheckPlane(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            // Unknown how this works at this time, but there is a Spell Effect called "ChangePlane". Could be related.
            // TODO: Investigate further.

            // Returning true by default as many mounts used this
            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Unknown11)]
        private static bool PrerequisiteCheckUnknown11(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            // Unknown how this works at this time.
            // TODO: Investigate further.

            // Returning true by default as many mounts used this
            return true;
        }

        [PrerequisiteCheck(PrerequisiteType.Unhealthy)]
        private static bool PrerequesiteCheckUnhealthy(Player player, PrerequisiteComparison comparison, uint value, uint objectId)
        {
            // TODO: Investigate further. Unknown what the value and objectId refers to at this time.
            
            // Error message is "Cannot recall while in Unhealthy Time" when trying to use Rapid Transport & other recall spells
            switch (comparison)
            {
                case PrerequisiteComparison.NotEqual:
                    if (player.Health != player.MaxHealth)
                        return false;
                    else
                        return true;
                default:
                    return true;
            }
        }
    }
}
