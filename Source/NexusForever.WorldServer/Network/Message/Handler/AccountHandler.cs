using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Storefront;
using NexusForever.WorldServer.Network.Message.Model;
using System;

namespace NexusForever.WorldServer.Network.Message.Handler
{
    public static class AccountHandler
    {
        [MessageHandler(GameMessageOpcode.ClientStorefrontRequestCatalog)]
        public static void HandleStorefrontRequestCatalogRealm(WorldSession session, ClientStorefrontRequestCatalog storefrontRequest)
        {
            // Packet order below, for reference and implementation

            // 0x096D - Account inventory

            // 0x0974 - Server Account Item Cooldowns (Boom Box!)
            
            // 0x0968 - Entitlements

            // 0x097F - Account Tier (Basic/Signature)

            // 0x0966 - SetAccountCurrencyAmounts

            // 0x096F - Weekly Omnibit progress

            // 0x096E - Daily Rewards packet
                // 0x078F - Claim Reward Button

            // 0x0981 - Unknown

            // Store packets
            // 0x0988 - Store catalogue categories 
            // 0x098B - Store catalogue offer grouips + offers
            // 0x0987 - Store catalogue finalised message
            GlobalStorefrontManager.Instance.HandleCatalogRequest(session);
        }

        [MessageHandler(GameMessageOpcode.ClientStorefrontRequestPurchaseHistory)]
        public static void HandleStorefrontRequestPurchaseHistory(WorldSession session, ClientStorefrontRequestPurchaseHistory requestPurchaseHistory)
        {
            session.EnqueueMessageEncrypted(new ServerStorePurchaseHistory
            {
                Purchases = new System.Collections.Generic.List<ServerStorePurchaseHistory.Purchase>
                {
                    new ServerStorePurchaseHistory.Purchase
                    {
                        PurchaseId = 51351958,
                        TimeSincePurchaseInDays = Double.Epsilon + 1d * -1d,
                        CurrencyId = Game.Account.Static.AccountCurrencyType.Omnibit,
                        Unknown0 = 0,
                        Cost = 200,
                        Name = "Magic Item",
                        Unknown1 = false,
                        Unknown2 = 0,
                        Unknown3 = 1
                    },
                    new ServerStorePurchaseHistory.Purchase
                    {
                        PurchaseId = 1000001,
                        TimeSincePurchaseInDays = (DateTime.UtcNow.Subtract(new DateTime(2019, 12, 31)).TotalMilliseconds * 1000) * 0.1d,
                        CurrencyId = Game.Account.Static.AccountCurrencyType.Omnibit,
                        Unknown0 = 0,
                        Cost = 300,
                        Name = "Magic Item 2",
                        Unknown1 = true,
                        Unknown2 = 0,
                        Unknown3 = 1
                    },
                    new ServerStorePurchaseHistory.Purchase
                    {
                        PurchaseId = 1000003,
                        TimeSincePurchaseInDays = -2d,
                        CurrencyId = Game.Account.Static.AccountCurrencyType.Omnibit,
                        Unknown0 = 1,
                        Cost = 400,
                        Name = "Magic Item 3",
                        Unknown1 = false,
                        Unknown2 = 0,
                        Unknown3 = 1
                    },
                    //new ServerStorePurchaseHistory.Purchase
                    //{
                    //    PurchaseId = 1000000,
                    //    TimeSincePurchaseInDays = -3.2d,
                    //    CurrencyId = Game.Account.Static.AccountCurrencyType.NCoin,
                    //    Unknown0 = 0,
                    //    Cost = 200,
                    //    Name = "Magic Item 4",
                    //    Unknown1 = false,
                    //    Unknown2 = 0,
                    //    Unknown3 = 0
                    //}
                }
            });
        }
    }
}