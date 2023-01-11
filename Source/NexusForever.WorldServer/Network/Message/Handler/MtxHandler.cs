using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Storefront;
using NexusForever.WorldServer.Game.Storefront.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;
using System;

namespace NexusForever.WorldServer.Network.Message.Handler
{
    public static class MtxHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

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

        [MessageHandler(GameMessageOpcode.ClientStorefrontPurchaseGift)]
        public static void HandleStorefrontPurchaseGift(WorldSession session, ClientStorefrontPurchaseGift storefrontPurchaseGift)
        {
            // TODO: Add Support for sending Items to other characters
            session.EnqueueMessageEncrypted(new ServerStoreError
            {
                ErrorCode = StoreError.GenericFail
            });
        }

        [MessageHandler(GameMessageOpcode.ClientStorefrontPurchase)]
        public static void HandleStorefrontPurchase(WorldSession session, ClientStorefrontPurchase storefrontPurchase)
        {
            static StoreError CheckForPurchase(ClientStorefrontPurchase storefrontPurchase, OfferItem offerItem)
            {
                if (offerItem == null)
                    return StoreError.InvalidOffer;

                OfferItemPrice itemPrice = offerItem.GetPriceDataForCurrency(storefrontPurchase.StorefrontPurchase.CurrencyType);
                if (itemPrice.GetCurrencyValue() > storefrontPurchase.StorefrontPurchase.Cost ||
                    itemPrice.GetCurrencyValue() < storefrontPurchase.StorefrontPurchase.Cost)
                    return StoreError.InvalidPrice;

                return StoreError.Success;
            }

            // Get OfferItem from Store
            OfferItem offerItem = GlobalStorefrontManager.Instance.GetStoreOfferItem(storefrontPurchase.StorefrontPurchase.OfferId);

            // Confirm no initial Errors with the data sent in packet
            StoreError errorCheck = CheckForPurchase(storefrontPurchase, offerItem);
            if (errorCheck != StoreError.Success)
            {
                session.EnqueueMessageEncrypted(new ServerStoreError
                {
                    ErrorCode = errorCheck
                });
                return;
            }

            // Checks passed; Make the purchase
            session.PurchaseManager.PurchaseOffer(offerItem, storefrontPurchase.StorefrontPurchase.CurrencyType);
        }

        [MessageHandler(GameMessageOpcode.ClientStorefrontRequestPurchaseHistory)]
        public static void HandleStorefrontRequestPurchaseHistory(WorldSession session, ClientStorefrontRequestPurchaseHistory requestPurchaseHistory)
        {
            session.PurchaseManager.SendPurchaseHistory();
        }

        [MessageHandler(GameMessageOpcode.ClientFortuneOpen)]
        public static void HandleClientFortuneOpen(WorldSession session, ClientFortuneOpen fortuneStartRequest)
        {
            session.EnqueueMessageEncrypted(new ServerFortuneRollResult());
            session.EnqueueMessageEncrypted(new ServerFortuneInit
            {
                Items = new System.Collections.Generic.List<uint>
                {
                    81269,
                    84623,
                    84625,
                    84624,
                    84626,
                    90924,
                    90997,
                    90996,
                    90950,
                    90833,
                    90840,
                    90948,
                    90794,
                    84621,
                    90949,
                    90951,
                    90834,
                    90801,
                    90839,
                    90838,
                    90836,
                    90835,
                    90837
                }
            });
        }

        [MessageHandler(GameMessageOpcode.ClientFortuneRollRequest)]
        public static void HandleClientFortuneOpen(WorldSession session, ClientFortuneRollRequest rollRequest)
        {
            // Error Handling seems to be done upfront by client. i.e. No spending without coins available.
            // No error handler seems to exist in LUA to deal with server error responses.

            session.EnqueueMessageEncrypted(new ServerFortuneRollResult
            {
                Unknown3 = 1,
            });
        }
    }
}