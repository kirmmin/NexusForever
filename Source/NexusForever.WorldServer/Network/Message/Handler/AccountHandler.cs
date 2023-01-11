using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Storefront;
using NexusForever.WorldServer.Game.Storefront.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;
using System;

namespace NexusForever.WorldServer.Network.Message.Handler
{
    public static class AccountHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        [MessageHandler(GameMessageOpcode.ClientAccountItemBind)]
        public static void HandleAccountItemBind(WorldSession session, ClientAccountItemBind accountItemBind)
        {
            session.AccountInventory.BindItem(accountItemBind.UserInventoryId);
        }
    }
}