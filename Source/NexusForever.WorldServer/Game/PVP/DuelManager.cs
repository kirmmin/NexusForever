using NexusForever.Shared;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.Social;
using NexusForever.WorldServer.Network;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NexusForever.WorldServer.Game.PVP
{
    public sealed class DuelManager: Singleton<DuelManager>, IUpdate
    {
        /// <summary>
        /// Id to be assigned to the next duel instance.
        /// </summary>
        public ulong NextDuelId => nextDuelId++;
        private ulong nextDuelId = 0;

        private Dictionary<ulong, Duel> duels = new Dictionary<ulong, Duel>();
        private HashSet<Duel> finishedDuels = new HashSet<Duel>();

        private UpdateTimer cleanTimer = new UpdateTimer(60d, true);

        private DuelManager()
        {
        }

        public void Update(double lastTick)
        {
            foreach (Duel activeDuel in duels.Values)
            {
                activeDuel.Update(lastTick);

                if (activeDuel.IsFinished)
                    finishedDuels.Add(activeDuel);
            }

            cleanTimer.Update(lastTick);
            if (cleanTimer.HasElapsed)
            {
                foreach (Duel finishedDuel in finishedDuels)
                    if (duels.ContainsKey(finishedDuel.Id))
                        duels.Remove(finishedDuel.Id);

                finishedDuels.Clear();
                cleanTimer.Reset();
            }
        }

        public Duel GetPendingDuel(ulong recipientId)
        {
            return duels.Values.FirstOrDefault(e => e.RecipientId == recipientId && e.IsPending);
        }

        public Duel GetActiveDuel(ulong characterId)
        {
            return duels.Values.FirstOrDefault(e => e.IsInProgress  && (e.ChallengerId == characterId || e.RecipientId == characterId));
        }

        public bool HasDuel(ulong characterId)
        {
            return duels.Values.FirstOrDefault(e => (e.IsInProgress || e.IsPending) && (e.ChallengerId == characterId || e.RecipientId == characterId)) != null;
        }

        public void CreateDuelChallenge(WorldSession session)
        {
            if (session.Player.TargetGuid == 0)
            {
                session.Player.SendSystemMessage($"You must have a target to challenge to a duel.");
                return;
            }

            if (HasDuel(session.Player.CharacterId) || HasDuel(session.Player.TargetGuid))
            {
                session.Player.SendSystemMessage($"You cannot make a duel request when you already have a pending duel.");
                return;
            }

            Player targetPlayer = session.Player.GetVisible<Player>(session.Player.TargetGuid);
            if (targetPlayer != null)
                CreateDuel(session.Player, targetPlayer);
        }

        private void CreateDuel(Player challenger, Player recipient)
        {
            Simple flag = SummonFlag(challenger.Map, recipient.Position, challenger.Faction1);
            if (flag == null)
                throw new InvalidOperationException("flag");

            SocialManager.Instance.SendMessage(challenger.Session, $"You have challenged {recipient.Name} to a duel.", channel: ChatChannel.System);
            SocialManager.Instance.SendMessage(recipient.Session, $"{challenger.Name} has challenged you to a duel.", channel: ChatChannel.System);
            SendDuelInvite(challenger, recipient);

            Duel duel = new Duel(NextDuelId, challenger, recipient, flag);
            duels.Add(duel.Id, duel);
        }

        private Simple SummonFlag(BaseMap map, Vector3 position, Faction faction)
        {
            uint creatureId = 0;

            switch (faction)
            {
                case Faction.Dominion:
                    creatureId = 47130;
                    break;
                case Faction.Exile:
                    creatureId = 47128;
                    break;
            }

            if (creatureId > 0)
            {
                Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(creatureId);
                if (entry == null)
                    throw new ArgumentNullException("entry");

                Simple flag = new Simple(entry);
                map.EnqueueAdd(flag, position);

                return flag;
            }

            return null;
        }

        public void DeclineDuelChallenge(Player player)
        {
            if (!HasDuel(player.CharacterId))
                throw new InvalidOperationException();

            Duel pendingDuel = GetPendingDuel(player.CharacterId);
            if (pendingDuel == null)
                throw new ArgumentNullException("pendingDuel");

            if (pendingDuel.Challenger != null && pendingDuel.Recipient != null)
                pendingDuel.Decline();
            else
            {
                // Finish duel with general error
                // duels.Finish();
                FinishDuel(pendingDuel);
            }
        }

        public void AcceptDuelChallenge(Player player)
        {
            if (!HasDuel(player.CharacterId))
                throw new InvalidOperationException();

            Duel pendingDuel = GetPendingDuel(player.CharacterId);
            if (pendingDuel == null)
                throw new ArgumentNullException("pendingDuel");

            if (pendingDuel.Challenger != null && pendingDuel.Recipient != null)
                pendingDuel.Accept();
            else
            {
                // Finish duel with people out of range
                // duels.Finish();
                FinishDuel(pendingDuel);
            }
        }

        private void FinishDuel(Duel duel)
        {
            duels.Remove(duel.Id);
            finishedDuels.Add(duel);
        }

        public void EndDuelsForPlayer(Player player)
        {
            if (HasDuel(player.CharacterId))
            {
                Duel pendingDuel = GetPendingDuel(player.CharacterId);
                if (pendingDuel != null)
                {
                    pendingDuel.Decline();
                    return;
                }

                Duel activeDuel = GetActiveDuel(player.CharacterId);
                if (activeDuel != null)
                {
                    Player winner = activeDuel.Challenger.CharacterId == player.CharacterId ? activeDuel.Recipient : activeDuel.Challenger;
                    activeDuel.End(winner, player);
                    return;
                }
            }
        }

        private void SendDuelInvite(Player challenger, Player recipient)
        {
            recipient.Session.EnqueueMessageEncrypted(new ServerDuelInvite
            {
                ChallengerId = challenger.Guid,
                OpponentId = recipient.Guid
            });
        }
    }
}
