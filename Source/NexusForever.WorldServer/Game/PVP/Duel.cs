using NexusForever.Shared;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.PVP.Static;
using NexusForever.WorldServer.Game.Social;
using NexusForever.WorldServer.Network.Message.Model;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace NexusForever.WorldServer.Game.PVP
{
    public class Duel: IUpdate
    {
        public ulong Id { get; private set; }
        public Player Challenger { get; private set; }
        public Player Recipient { get; private set; }
        public ulong ChallengerId { get; private set; }
        public ulong RecipientId { get; private set; }
        public Simple Flag { get; private set; }

        public bool IsPending => state == DuelState.Pending;
        public bool IsInProgress => state == DuelState.InProgress || state == DuelState.Preparing;
        public bool IsFinished => state == DuelState.Finished;

        private uint flagGuid;
        private ulong WinnerId;
        private DuelState state;
        private UpdateTimer expireTimer = new UpdateTimer(30d);
        private UpdateTimer prepareTimer = new UpdateTimer(5d, false);
        private UpdateTimer checkTimer = new UpdateTimer(0.5d);
        private UpdateTimer challengerOorTimer = new UpdateTimer(10d, false);
        private UpdateTimer recipientOorTimer = new UpdateTimer(10d, false);

        public Duel(ulong duelId, Player challenger, Player recipient, Simple flag)
        {
            Id = duelId;
            Challenger = challenger;
            Recipient = recipient;
            ChallengerId = challenger.CharacterId;
            RecipientId = recipient.CharacterId;
            Flag = flag;

            state = DuelState.Pending;
        }

        public void Update(double lastTick)
        {
            if (state < DuelState.Pending || state == DuelState.Finished)
                return;

            if (state == DuelState.Pending)
            {
                expireTimer.Update(lastTick);
                if (expireTimer.HasElapsed)
                {
                    SocialManager.Instance.SendMessage(Challenger.Session, $"Your duel with {Recipient.Name} expired.", channel: ChatChannel.System);
                    SocialManager.Instance.SendMessage(Recipient.Session, $"{Challenger.Name}'s duel has expired.", channel: ChatChannel.System);

                    Flag.Map.EnqueueRemove(Flag);
                    state = DuelState.Finished;
                }
            }

            if (prepareTimer.IsTicking)
            {
                prepareTimer.Update(lastTick);
                if (prepareTimer.HasElapsed)
                {
                    Start();
                    prepareTimer.Reset(false);
                }
            }

            if (IsInProgress)
            {
                checkTimer.Update(lastTick);
                if (checkTimer.HasElapsed)
                {
                    // Check Range, and warn if necessary
                    CheckFlagOutOfRange(Challenger, challengerOorTimer);
                    CheckFlagOutOfRange(Recipient, recipientOorTimer);

                    checkTimer.Reset();
                }

                challengerOorTimer.Update(lastTick);
                if (challengerOorTimer.HasElapsed)
                {
                    // End Duel early
                    End(Recipient, Challenger, true);
                }

                recipientOorTimer.Update(lastTick);
                if (recipientOorTimer.HasElapsed)
                {
                    // End duel early
                    End(Challenger, Recipient, true);
                }
            }
        }

        public void Decline()
        {
            Challenger.DuelOpponentGuid = 0;
            Recipient.DuelOpponentGuid = 0;
            SocialManager.Instance.SendMessage(Recipient.Session, $"You've declined {Challenger.Name}'s challenge!");
            SocialManager.Instance.SendMessage(Challenger.Session, $"{Recipient.Name} has declined your challenge!");
            Flag.Map.EnqueueRemove(Flag);

            state = DuelState.Finished;
        }

        public void Accept()
        {
            Challenger.Session.EnqueueMessageEncrypted(new ServerDuelInvite
            {
                ChallengerId = Challenger.Guid,
                OpponentId = Recipient.Guid
            });
            Prepare();
        }

        private void Prepare()
        {
            flagGuid = Flag.Guid;

            Challenger.DuelOpponentGuid = Recipient.Guid;
            Recipient.DuelOpponentGuid = Challenger.Guid;

            PrepareForPlayer(Challenger);
            PrepareForPlayer(Recipient);
            prepareTimer.Resume();

            state = DuelState.Preparing;
        }

        private void PrepareForPlayer(Player player)
        {
            uint enemyGuid = Challenger.Guid == player.Guid ? Recipient.Guid : Challenger.Guid;

            player.DuelOpponentGuid = enemyGuid;
            player.Session.EnqueueMessageEncrypted(new ServerDuelStart
            {
                ChallengerId = player.Guid,
                OpponentId = enemyGuid
            });
        }
       
        private void Start()
        {
            Challenger.Session.EnqueueMessageEncrypted(new ServerDuelBegin
            {
                ChallengerId = Challenger.Guid,
                OpponentId = Recipient.Guid
            });
            Recipient.Session.EnqueueMessageEncrypted(new ServerDuelBegin
            {
                ChallengerId = Recipient.Guid,
                OpponentId = Challenger.Guid
            });

            Challenger.SetPvPFlags(PvPFlag.Enabled);
            Challenger.AddToThreatList(Recipient.Guid);
            Recipient.SetPvPFlags(PvPFlag.Enabled);
            Recipient.AddToThreatList(Challenger.Guid);

            state = DuelState.InProgress;
        }

        private void CheckFlagOutOfRange(Player player, UpdateTimer timer)
        {
            bool OutOfRange = Vector3.Distance(player.Position, Flag.Position) > 30f;

            if (!OutOfRange)
            {
                if (timer.IsTicking)
                {
                    player.Session.EnqueueMessageEncrypted(new ServerDuelCancelWarning());
                    timer.Reset(false);
                }
                return;
            }

            if (!timer.IsTicking)
            {
                player.Session.EnqueueMessageEncrypted(new ServerDuelLeftArea());
                timer.Reset();
            }
        }

        public void End(Player winner, Player loser, bool leftArea = false)
        {
            WinnerId = winner.CharacterId;

            Challenger.DuelOpponentGuid = 0;
            Recipient.DuelOpponentGuid = 0;

            byte result = 0;
            // Calculate winner
            if (leftArea)
                result = 3;

            SendEndResult(winner, winner.Guid, loser.Guid, result);
            SendEndResult(loser, winner.Guid, loser.Guid, result);

            SocialManager.Instance.SendMessage(winner.Session, $"You have defeated {loser.Name} in a duel.");
            SocialManager.Instance.SendMessage(loser.Session, $"You have defeated {loser.Name} in a duel.");

            winner.CastSpell(70355, new Spell.SpellParameters
            {
                UserInitiatedSpellCast = false
            });
            loser.CastSpell(70356, new Spell.SpellParameters
            {
                UserInitiatedSpellCast = false
            });
            Flag.Map.EnqueueRemove(Flag);

            Challenger.SetPvPFlags(PvPFlag.Disabled);
            Challenger.RemoveFromThreatList(Recipient.Guid);
            Recipient.SetPvPFlags(PvPFlag.Disabled);
            Recipient.RemoveFromThreatList(Challenger.Guid);

            state = DuelState.Finished;
        }

        private void SendEndResult(Player player, uint winnerGuid, uint loserGuid, byte result)
        {
            player.Session?.EnqueueMessageEncrypted(new ServerDuelEnd
            {
                WinnerId = winnerGuid,
                LoserId = loserGuid,
                Result = result
            });
        }
    }
}
