using NexusForever.Shared.Game.Events;
using NexusForever.Shared.GameTable;
using NexusForever.WorldServer.Game;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Housing;

namespace NexusForever.WorldServer.Script.Creature.City
{
    [Script(54400)] // Zen Pond (Thayd)
    [Script(54401)] // Windmill (Thayd)
    [Script(54403)] // Power Generator (Thayd)
    [Script(54404)] // Storage Unit (Thayd)
    [Script(65296)] // Zen Pond (Illium)
    [Script(65297)] // Windmill (Illium)
    [Script(65298)] // Power Generator (Illium)
    [Script(65299)] // Storage Unit (Illium)
    public class HousingIntroEntities : CreatureScript
    {
        public override void OnActivateSuccess(WorldEntity me, WorldEntity activator)
        {
            base.OnActivateSuccess(me, activator);

            if (!(activator is Player player))
                return;

            switch (me.CreatureId)
            {
                case 54400:
                case 65296:
                    StoryBuilder.Instance.SendStoryPanel(GameTableManager.Instance.StoryPanel.GetEntry(2291), player);
                    break;
                case 54401:
                case 65297:
                    StoryBuilder.Instance.SendStoryPanel(GameTableManager.Instance.StoryPanel.GetEntry(2294), player);
                    break;
                case 54403:
                case 65298:
                    StoryBuilder.Instance.SendStoryPanel(GameTableManager.Instance.StoryPanel.GetEntry(2292), player);
                    break;
                case 54404:
                case 65299:
                    StoryBuilder.Instance.SendStoryPanel(GameTableManager.Instance.StoryPanel.GetEntry(2293), player);
                    break;
            }
        }
    }
}
