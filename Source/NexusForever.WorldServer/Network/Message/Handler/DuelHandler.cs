using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Shared.Game.Events;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Database.Character;
using NexusForever.WorldServer.Database.Character.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.PVP;
using NexusForever.WorldServer.Game.Social;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using NLog;

namespace NexusForever.WorldServer.Network.Message.Handler
{
    public static class DuelHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        [MessageHandler(GameMessageOpcode.ClientDuelRequest)]
        public static void HandleDuelRequest(WorldSession session, ClientDuelRequest duelRequest)
        {
            DuelManager.Instance.CreateDuelChallenge(session);
        }

        [MessageHandler(GameMessageOpcode.ClientDuelDecline)]
        public static void HandleDuelDecline(WorldSession session, ClientDuelDecline duelDecline)
        {
            DuelManager.Instance.DeclineDuelChallenge(session.Player);
        }

        [MessageHandler(GameMessageOpcode.ClientDuelAccept)]
        public static void HandleDuelAccept(WorldSession session, ClientDuelAccept duelAccept)
        {
            DuelManager.Instance.AcceptDuelChallenge(session.Player);
        }
    }
}
