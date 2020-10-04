using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.GameTable.Static;
using NexusForever.WorldServer.Game.Entity;
using System.Text;

namespace NexusForever.WorldServer.Command.Shared
{
    public static class EntityUtility
    {
        public static void BuildHeader(StringBuilder builder, WorldEntity target, Language language)
        {
            builder.AppendLine("=============================");
            builder.AppendLine($"UnitId: {target.Guid} | DB ID: {target.EntityId} | Type: {target.Type} | CreatureID: {target.CreatureId} | Name: {GetName(target, language)}");
        }

        public static string GetName(WorldEntity target, Language language)
        {
            if (target is Player player)
                return player.Name;

            Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(target.CreatureId);
            return GameTableManager.Instance.GetTextTable(language).GetEntry(entry.LocalizedTextIdName) ?? "Unknown";
        }
    }
}
