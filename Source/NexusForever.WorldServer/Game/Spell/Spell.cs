using System;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Shared;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Prerequisite;
using NexusForever.WorldServer.Game.Spell.Event;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NLog;

namespace NexusForever.WorldServer.Game.Spell
{
    public partial class Spell : IUpdate
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public uint CastingId { get; }
        public bool IsCasting => status == SpellStatus.Casting;
        public bool IsFinished => status == SpellStatus.Finished;
        public bool IsFailed => status == SpellStatus.Failed;
        public bool IsWaiting => status == SpellStatus.Waiting;
        public uint Spell4Id => parameters.SpellInfo.Entry.Id;
        public bool HasThresholdToCast => thresholdSpells.Count > 0;
        public CastMethod CastMethod { get; }

        private readonly UnitEntity caster;
        private readonly SpellParameters parameters;
        private SpellStatus status;

        private readonly List<Spell> thresholdSpells = new List<Spell>();

        private readonly List<SpellTargetInfo> targets = new List<SpellTargetInfo>();

        private readonly SpellEventManager events = new SpellEventManager();

        public Spell(UnitEntity caster, SpellParameters parameters)
        {
            this.caster     = caster;
            this.parameters = parameters;
            CastingId       = GlobalSpellManager.Instance.NextCastingId;
            status          = SpellStatus.Initiating;
            CastMethod      = (CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod;

            if (parameters.RootSpellInfo == null)
                parameters.RootSpellInfo = parameters.SpellInfo;
        }

        public void Update(double lastTick)
        {
            events.Update(lastTick);

            // Prevent Mounts from Ending instantly
            // TODO: Reference this spell to Mount Entity and finish spell when Mount is removed?
            if (parameters.SpellInfo.BaseInfo.Entry.Spell4SpellTypesIdSpellType == 30) // This also happens with the Mount Unlock items. Investigate further.
                return;

            if (status == SpellStatus.Executing && HasThresholdToCast)
                status = SpellStatus.Waiting;

            if (status == SpellStatus.Waiting && HasThresholdToCast)
            {
                thresholdSpells.ForEach(s => s.Update(lastTick));

                foreach (Spell thresholdSpell in thresholdSpells.ToList())
                    if (thresholdSpell.IsFinished)
                        thresholdSpells.Remove(thresholdSpell);

                if (thresholdSpells.Count == 0)
                    SetCooldown();
            }

            if ((status == SpellStatus.Executing && !events.HasPendingEvent) || 
                (status == SpellStatus.Waiting && !HasThresholdToCast))
            {
                // spell effects have finished executing
                status = SpellStatus.Finished;
                log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has finished.");

                // TODO: add a timer to count down on the Effect before sending the finish - sending the finish will e.g. wear off the buff
                SendSpellFinish();

                if (caster is Player player)
                    if (parameters.ThresholdValue > 0 && parameters.ThresholdMaxValue > 0 && parameters.ThresholdValue == parameters.ThresholdMaxValue)
                        player.Session.EnqueueMessageEncrypted(new ServerSpellThresholdClear
                        {
                            Spell4Id = parameters.ParentSpellInfo?.Entry.Id ?? 0u
                        });
            }
        }

        /// <summary>
        /// Begin cast, checking prerequisites before initiating.
        /// </summary>
        public void Cast()
        {
            /** Existing Spell **/
            if (status == SpellStatus.Waiting)
            {
                HandleThresholdCast();
                return;
            }

            /** New Spell **/
            if (status != SpellStatus.Initiating)
                throw new InvalidOperationException();

            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started initating.");

            CastResult result = CheckCast();
            if (result != CastResult.Ok)
            {
                log.Trace($"Spell {parameters.SpellInfo.Entry.Id} failed to cast {result}.");

                SendSpellCastResult(result);
                status = SpellStatus.Failed;
                return;
            }

            InitialiseThresholdSpells();

            if (caster is Player player)
                if (parameters.SpellInfo.GlobalCooldown != null && parameters.UserInitiatedSpellCast) // Using UserInitiatedSpellCast to prevent proxies affecting the GCD
                    player.SpellManager.SetGlobalSpellCooldown(parameters.SpellInfo.GlobalCooldown.CooldownTime / 1000d);    

            status = SpellStatus.Casting;
            SendSpellStart();

            InitialiseCastMethod();
            
            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started casting.");
        }

        private CastResult CheckCast()
        {
            if (!CheckPrerequisites())
                return CastResult.SpellPreRequisites;

            CastResult ccResult = CheckCCConditions();
            if (ccResult != CastResult.Ok)
                return ccResult;

            if (caster is Player player)
            {
                if (player.SpellManager.GetSpellCooldown(parameters.SpellInfo.Entry.Id) > 0d)
                    return CastResult.SpellCooldown;

                // this isn't entirely correct, research GlobalCooldownEnum
                if (parameters.SpellInfo.Entry.GlobalCooldownEnum == 0
                    && player.SpellManager.GetGlobalSpellCooldown() > 0d)
                    return CastResult.SpellGlobalCooldown;

                if (parameters.SpellInfo.Entry.PrerequisiteIdCasterCast > 0 && !PrerequisiteManager.Instance.Meets(player, parameters.SpellInfo.Entry.PrerequisiteIdCasterCast))
                    return CastResult.PrereqCasterCast;
            }

            return CastResult.Ok;
        }

        private bool CheckPrerequisites()
        {
            // TODO
            if (parameters.SpellInfo.CasterCastPrerequisites != null)
            {
            }

            // not sure if this should be for explicit and/or implicit targets
            if (parameters.SpellInfo.TargetCastPrerequisites != null)
            {
            }

            // this probably isn't the correct place, name implies this should be constantly checked
            if (parameters.SpellInfo.CasterPersistencePrerequisites != null)
            {
            }

            if (parameters.SpellInfo.TargetPersistencePrerequisites != null)
            {
            }

            return true;
        }

        private CastResult CheckCCConditions()
        {
            // TODO: this just looks like a mask for CCState enum
            if (parameters.SpellInfo.CasterCCConditions != null)
            {
            }

            // not sure if this should be for explicit and/or implicit targets
            if (parameters.SpellInfo.TargetCCConditions != null)
            {
            }

            return CastResult.Ok;
        }

        private void InitialiseThresholdSpells()
        {
            if (parameters.SpellInfo.Thresholds.Count == 0)
                return;

            foreach (Spell4ThresholdsEntry thresholdsEntry in parameters.SpellInfo.Thresholds)
            {
                Spell4Entry spell4Entry = GameTableManager.Spell4.GetEntry(thresholdsEntry.Spell4IdToCast);
                if (spell4Entry == null)
                    throw new ArgumentOutOfRangeException();

                SpellBaseInfo spellBaseInfo = GlobalSpellManager.GetSpellBaseInfo(spell4Entry.Spell4BaseIdBaseSpell);
                if (spellBaseInfo == null)
                    throw new ArgumentOutOfRangeException();

                SpellInfo spellInfo = spellBaseInfo.GetSpellInfo((byte)spell4Entry.TierIndex);
                if (spellInfo == null)
                    throw new ArgumentOutOfRangeException();

                Spell childSpell = new Spell(caster, new SpellParameters
                {
                    SpellInfo = spellInfo,
                    ParentSpellInfo = parameters.SpellInfo,
                    RootSpellInfo = parameters.RootSpellInfo,
                    ThresholdValue = thresholdsEntry.OrderIndex + 1,
                    ThresholdMaxValue = (uint)parameters.SpellInfo.Thresholds.Count
                });
                thresholdSpells.Add(childSpell);

                log.Trace($"Added Child Spell {childSpell.Spell4Id} with casting ID {childSpell.CastingId} to parent casting ID {CastingId}");
            }

            // If ThresholdTime exceeded before spell completion, finish this spell cast immediately
            events.EnqueueEvent(new SpellEvent(parameters.SpellInfo.Entry.CastTime / 1000d + parameters.SpellInfo.Entry.ThresholdTime / 1000d, Finish));
        }

        private void InitialiseCastMethod()
        {
            switch (CastMethod)
            {
                case CastMethod.Channeled:
                case CastMethod.ChanneledField:
                    events.EnqueueEvent(new SpellEvent(parameters.SpellInfo.Entry.ChannelInitialDelay / 1000d, Execute)); // Execute after initial delay
                    events.EnqueueEvent(new SpellEvent(parameters.SpellInfo.Entry.ChannelMaxTime / 1000d, Finish)); // End Spell Cast

                    uint numberOfPulses = (parameters.SpellInfo.Entry.ChannelMaxTime - parameters.SpellInfo.Entry.ChannelInitialDelay) / parameters.SpellInfo.Entry.ChannelPulseTime; // Calculate number of "ticks" in this spell cast

                    // Add ticks at each pulse
                    for (int i = 1; i <= numberOfPulses; i++)
                        events.EnqueueEvent(new SpellEvent(parameters.SpellInfo.Entry.ChannelPulseTime * i / 1000d, () =>
                        {
                            SelectTargets();
                            ExecuteEffects();
                        }));
                    break;
                case CastMethod.Normal:
                case CastMethod.RapidTap:
                default:
                    events.EnqueueEvent(new SpellEvent(parameters.SpellInfo.Entry.CastTime / 1000d, Execute)); // enqueue spell to be executed after cast time
                    break;
            }
        }

        private void HandleThresholdCast()
        {
            if (status != SpellStatus.Waiting)
                throw new InvalidOperationException();

            if (thresholdSpells.Count == 0)
                throw new InvalidOperationException();

            CastResult result = CheckCast();
            CastResult childResult = thresholdSpells[0].CheckCast();
            if (result != CastResult.Ok || childResult != CastResult.Ok)
            {
                SendSpellCastResult(result);
                return;
            }

            thresholdSpells[0].Cast();
        }

        /// <summary>
        /// Cancel cast with supplied <see cref="CastResult"/>.
        /// </summary>
        public void CancelCast(CastResult result)
        {
            if (status != SpellStatus.Casting && !HasThresholdToCast)
                throw new InvalidOperationException();

            if (HasThresholdToCast)
                if (thresholdSpells[0].IsCasting)
                {
                    thresholdSpells[0].CancelCast(result);
                    return;
                }

            if (caster is Player player && !player.IsLoading)
            {
                player.Session.EnqueueMessageEncrypted(new Server07F9
                {
                    ServerUniqueId = CastingId,
                    CastResult     = result,
                    CancelCast     = true
                });
            }

            events.CancelEvents();
            status = SpellStatus.Executing;

            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} cast was cancelled.");
        }

        private void Execute()
        {
            status = SpellStatus.Executing;
            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started executing.");

            if (!HasThresholdToCast)
                SetCooldown();

            SelectTargets();
            ExecuteEffects();

            SendSpellGo();

            if (caster is Player player && !player.IsLoading)
            {
                // TODO: Confirm whether RapidTap spells cancel others out, and figure out slight bug in this logic where the spells don't cancel properly
                //if (CastMethod == CastMethod.RapidTap && parameters.ParentSpellInfo == null)
                //    if (player.HasSpell(CastMethod, out Spell pendingRapidTap))
                //        if (pendingRapidTap.Spell4Id != Spell4Id)
                //            pendingRapidTap.FinishInstantly();

                if (parameters.SpellInfo.Entry.ThresholdTime > 0)
                    player.Session.EnqueueMessageEncrypted(new ServerSpellThresholdStart
                    {
                        Spell4Id = parameters.SpellInfo.Entry.Id,
                        RootSpell4Id = parameters.RootSpellInfo?.Entry.Id ?? 0,
                        ParentSpell4Id = parameters.ParentSpellInfo?.Entry.Id ?? 0,
                        CastingId = CastingId
                    });

                if (parameters.ThresholdValue > 0)
                    player.Session.EnqueueMessageEncrypted(new ServerSpellThresholdUpdate
                    {
                        Spell4Id = parameters.ParentSpellInfo?.Entry.Id ?? 0u,
                        Value = (byte)parameters.ThresholdValue
                    });
            }
        }

        private void SetCooldown()
        {
            if (caster is Player player)
                if (parameters.SpellInfo.Entry.SpellCoolDown != 0u)
                    player.SpellManager.SetSpellCooldown(parameters.SpellInfo.Entry.Id, parameters.SpellInfo.Entry.SpellCoolDown / 1000d);
        }

        private void SelectTargets()
        {
            targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Caster, caster));
            foreach (TelegraphDamageEntry telegraphDamageEntry in parameters.SpellInfo.Telegraphs)
            {
                var telegraph = new Telegraph(telegraphDamageEntry, caster, caster.Position, caster.Rotation);
                foreach (UnitEntity entity in telegraph.GetTargets())
                    targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Telegraph, entity));
            }
        }

        private void ExecuteEffects()
        {
            foreach (Spell4EffectsEntry spell4EffectsEntry in parameters.SpellInfo.Effects)
            {
                if (caster is Player player)
                {
                    // Ensure caster can apply this effect
                    if (spell4EffectsEntry.PrerequisiteIdCasterApply > 0 && !PrerequisiteManager.Instance.Meets(player, spell4EffectsEntry.PrerequisiteIdCasterApply))
                        continue;
                }
                
                // select targets for effect
                List<SpellTargetInfo> effectTargets = targets
                    .Where(t => (t.Flags & (SpellEffectTargetFlags)spell4EffectsEntry.TargetFlags) != 0)
                    .ToList();

                SpellEffectDelegate handler = GlobalSpellManager.Instance.GetEffectHandler((SpellEffectType)spell4EffectsEntry.EffectType);
                if (handler == null)
                    log.Warn($"Unhandled spell effect {(SpellEffectType)spell4EffectsEntry.EffectType}");
                else
                {
                    uint effectId = GlobalSpellManager.Instance.NextEffectId;
                    foreach (SpellTargetInfo effectTarget in effectTargets)
                    {
                        var info = new SpellTargetInfo.SpellTargetEffectInfo(effectId, spell4EffectsEntry);
                        effectTarget.Effects.Add(info);

                        // TODO: if there is an unhandled exception in the handler, there will be an infinite loop on Execute()
                        handler.Invoke(this, effectTarget.Entity, info);
                    }
                }

                // Add durations for each effect so that when the Effect timer runs out, the Spell can Finish.
                if (spell4EffectsEntry.DurationTime > 0)
                    events.EnqueueEvent(new SpellEvent(spell4EffectsEntry.DurationTime / 1000d, () => { /* placeholder for duration */ }));
            }
        }

        public bool IsMovingInterrupted()
        {
            // TODO: implement correctly
            return parameters.SpellInfo.Entry.CastTime > 0;
        }

        public void Finish()
        {
            if (status == SpellStatus.Executing || status == SpellStatus.Waiting)
                SetCooldown();

            foreach (Spell thresholdSpell in thresholdSpells.ToList())
            {
                if (thresholdSpell.IsCasting && !thresholdSpell.IsFinished)
                    thresholdSpell.CancelCast(CastResult.SpellCancelled);

                thresholdSpells.Remove(thresholdSpell);
            }

            events.CancelEvents();
            status = SpellStatus.Executing;
        }

        private void SendSpellCastResult(CastResult castResult)
        {
            if (castResult == CastResult.Ok)
                return;

            if (caster is Player player && !player.IsLoading)
            {
                player.Session.EnqueueMessageEncrypted(new ServerSpellCastResult
                {
                    Spell4Id   = parameters.SpellInfo.Entry.Id,
                    CastResult = castResult
                });
            }
        }

        private void SendSpellStart()
        {
            caster.EnqueueToVisible(new ServerSpellStart
            {
                CastingId              = CastingId,
                CasterId               = caster.Guid,
                PrimaryTargetId        = caster.Guid,
                Spell4Id               = parameters.SpellInfo.Entry.Id,
                RootSpell4Id           = parameters.RootSpellInfo?.Entry.Id ?? 0,
                ParentSpell4Id         = parameters.ParentSpellInfo?.Entry.Id ?? 0,
                FieldPosition          = new Position(caster.Position),
                UserInitiatedSpellCast = parameters.UserInitiatedSpellCast
            }, true);
        }

        private void SendSpellFinish()
        {
            if (status != SpellStatus.Finished)
                return;

            caster.EnqueueToVisible(new ServerSpellFinish
            {
                ServerUniqueId = CastingId,
            }, true);
        }

        private void SendSpellGo()
        {
            var serverSpellGo = new ServerSpellGo
            {
                ServerUniqueId     = CastingId,
                PrimaryDestination = new Position(caster.Position),
                Phase              = -1
            };

            foreach (SpellTargetInfo targetInfo in targets
                .Where(t => t.Effects.Count > 0))
            {
                var networkTargetInfo = new ServerSpellGo.TargetInfo
                {
                    UnitId        = targetInfo.Entity.Guid,
                    TargetFlags   = 1,
                    InstanceCount = 1,
                    CombatResult  = CombatResult.Hit
                };

                foreach (SpellTargetInfo.SpellTargetEffectInfo targetEffectInfo in targetInfo.Effects)
                {
                    var networkTargetEffectInfo = new ServerSpellGo.TargetInfo.EffectInfo
                    {
                        Spell4EffectId = targetEffectInfo.Entry.Id,
                        EffectUniqueId = targetEffectInfo.EffectId,
                        TimeRemaining  = -1
                    };

                    if (targetEffectInfo.Damage != null)
                    {
                        networkTargetEffectInfo.InfoType = 1;
                        networkTargetEffectInfo.DamageDescriptionData = new ServerSpellGo.TargetInfo.EffectInfo.DamageDescription
                        {
                            RawDamage          = targetEffectInfo.Damage.RawDamage,
                            RawScaledDamage    = targetEffectInfo.Damage.RawScaledDamage,
                            AbsorbedAmount     = targetEffectInfo.Damage.AbsorbedAmount,
                            ShieldAbsorbAmount = targetEffectInfo.Damage.ShieldAbsorbAmount,
                            AdjustedDamage     = targetEffectInfo.Damage.AdjustedDamage,
                            OverkillAmount     = targetEffectInfo.Damage.OverkillAmount,
                            KilledTarget       = targetEffectInfo.Damage.KilledTarget,
                            CombatResult       = targetEffectInfo.Damage.CombatResult,
                            DamageType         = targetEffectInfo.Damage.DamageType
                        };
                    }

                    networkTargetInfo.EffectInfoData.Add(networkTargetEffectInfo);
                }

                serverSpellGo.TargetInfoData.Add(networkTargetInfo);
            }

            caster.EnqueueToVisible(serverSpellGo, true);
        }
    }
}
