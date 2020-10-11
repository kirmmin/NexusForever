using System;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Shared;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Prerequisite;
using NexusForever.WorldServer.Game.Spell.Event;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;
using NexusForever.WorldServer.Network.Message.Model.Shared;
using NLog;

namespace NexusForever.WorldServer.Game.Spell
{
    public partial class Spell : IUpdate
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public uint CastingId { get; }
        public bool IsCasting => parameters.UserInitiatedSpellCast && status == SpellStatus.Casting || ((CastMethod == CastMethod.Channeled || CastMethod == CastMethod.ChanneledField) && (status == SpellStatus.Casting || status == SpellStatus.Executing));
        public bool IsFinished => status == SpellStatus.Finished;
        public bool IsFailed => status == SpellStatus.Failed;
        public bool IsWaiting => status == SpellStatus.Waiting;
        public uint Spell4Id => parameters.SpellInfo.Entry.Id;
        public bool HasThresholdToCast => (parameters.SpellInfo.Thresholds.Count > 0 && thresholdValue < thresholdMax) || thresholdSpells.Count > 0;
        public CastMethod CastMethod { get; }

        public bool IsClientSideInteraction => parameters.ClientSideInteraction != null;

        private readonly UnitEntity caster;
        private readonly SpellParameters parameters;
        private SpellStatus status;

        private double holdDuration;
        private uint totalThresholdTimer;
        private uint thresholdMax;
        private uint thresholdValue;
        private byte currentPhase = 255;
        private uint duration = 0;

        private readonly List<Spell> thresholdSpells = new List<Spell>();

        private readonly List<SpellTargetInfo> targets = new();

        private readonly SpellEventManager events = new();

        public Spell(UnitEntity caster, SpellParameters parameters)
        {
            this.caster     = caster;
            this.parameters = parameters;
            CastingId       = GlobalSpellManager.Instance.NextCastingId;
            status          = SpellStatus.Initiating;
            CastMethod      = (CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod;

            if (parameters.RootSpellInfo == null)
                parameters.RootSpellInfo = parameters.SpellInfo;

            if (parameters.SpellInfo.Thresholds.Count > 0)
                thresholdMax = (uint)parameters.SpellInfo.Thresholds.Count;
        }

        public void Update(double lastTick)
        {
            if (status == SpellStatus.Initiating)
                return;

            events.Update(lastTick);

            // Prevent Mounts from Ending instantly
            // TODO: Reference this spell to Mount Entity and finish spell when Mount is removed?
            if (parameters.SpellInfo.BaseInfo.Entry.Spell4SpellTypesIdSpellType == 30) // This also happens with the Mount Unlock items. Investigate further.
                return;

            if (status == SpellStatus.Executing && HasThresholdToCast)
                status = SpellStatus.Waiting;

            if (status == SpellStatus.Waiting && CastMethod == CastMethod.ChargeRelease)
            {
                holdDuration += lastTick;

                if (holdDuration >= totalThresholdTimer)
                    HandleThresholdCast();
            }

            thresholdSpells.ForEach(s => s.Update(lastTick));
            if (status == SpellStatus.Waiting && HasThresholdToCast)
            {
                foreach (Spell thresholdSpell in thresholdSpells.ToList())
                    if (thresholdSpell.IsFinished)
                        thresholdSpells.Remove(thresholdSpell);
            }

            if ((status == SpellStatus.Executing && !events.HasPendingEvent && !parameters.ForceCancelOnly) || 
                (status == SpellStatus.Waiting && !HasThresholdToCast) ||
                status == SpellStatus.Finishing)
            {
                // spell effects have finished 
                status = SpellStatus.Finished;
                caster.RemoveEffect(CastingId);
                log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has finished.");

                parameters.CompleteAction?.Invoke(parameters);

                foreach (SpellTargetInfo target in targets)
                    target.Entity?.RemoveSpellProperties(Spell4Id);

                if (caster is Player player)
                    if (thresholdMax > 0)
                    {
                        player.Session.EnqueueMessageEncrypted(new ServerSpellThresholdClear
                        {
                            Spell4Id = Spell4Id
                        });
                        
                        if (CastMethod != CastMethod.ChargeRelease)
                            SetCooldown();
                    }

                SendSpellFinish();
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

            CastResult result = CheckCast();
            if (result != CastResult.Ok)
            {
                SendSpellCastResult(result);
                status = SpellStatus.Failed;
                return;
            }

            if (caster is Player player)
                if (parameters.SpellInfo.GlobalCooldown != null && !parameters.IsProxy)
                    player.SpellManager.SetGlobalSpellCooldown(parameters.SpellInfo.GlobalCooldown.CooldownTime / 1000d);

            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started initating.");  

            double castTime = parameters.CastTimeOverride > 0 ? parameters.CastTimeOverride / 1000d : parameters.SpellInfo.Entry.CastTime / 1000d;
            if (parameters.ClientSideInteraction != null)
            {
                SendSpellStartClientInteraction();

                if ((CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod != CastMethod.ClientSideInteraction)
                    events.EnqueueEvent(new SpellEvent(castTime, SucceedClientInteraction));
            }
            else
            {
                SendSpellStart();
                InitialiseCastMethod();

                events.EnqueueEvent(new SpellEvent(castTime, Execute));
            }

            status = SpellStatus.Casting;
            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started casting.");
        }

        private CastResult CheckCast()
        {
            CastResult preReqResult = CheckPrerequisites();
            if (preReqResult != CastResult.Ok)
                return preReqResult;

            CastResult ccResult = CheckCCConditions();
            if (ccResult != CastResult.Ok)
                return ccResult;

            if (caster is Player player)
            {
                if (IsCasting)
                    return CastResult.SpellAlreadyCasting;

                // TODO: Some spells can be cast during other spell casts. Reflect that in this check
                if (caster.IsCasting() && !parameters.IsProxy)
                    return CastResult.SpellAlreadyCasting;

                if (player.SpellManager.GetSpellCooldown(parameters.SpellInfo.Entry.Id) > 0d)
                    return CastResult.SpellCooldown;

                // this isn't entirely correct, research GlobalCooldownEnum
                if (parameters.SpellInfo.Entry.GlobalCooldownEnum == 0 && player.SpellManager.GetGlobalSpellCooldown() > 0d && !parameters.IsProxy && parameters.UserInitiatedSpellCast)
                {
                    if (CastMethod != CastMethod.ChargeRelease)
                        return CastResult.SpellGlobalCooldown;
                }

                if (parameters.CharacterSpell?.MaxAbilityCharges > 0 && parameters.CharacterSpell?.AbilityCharges == 0)
                    return CastResult.SpellNoCharges;
                    
                if (parameters.SpellInfo.Entry.PrerequisiteIdCasterCast > 0 && !PrerequisiteManager.Instance.Meets(player, parameters.SpellInfo.Entry.PrerequisiteIdCasterCast))
                    return CastResult.PrereqCasterCast;

                for (int i = 0; i < parameters.SpellInfo.Entry.CasterInnateRequirements.Length; i++)
                {
                    uint innateRequirement = parameters.SpellInfo.Entry.CasterInnateRequirements[i];
                    if (innateRequirement == 0)
                        continue;

                    switch (parameters.SpellInfo.Entry.CasterInnateRequirementEval[i])
                    {
                        case 2:
                            if (caster.GetVitalValue((Vital)innateRequirement) < parameters.SpellInfo.Entry.CasterInnateRequirementValues[i])
                                return GlobalSpellManager.Instance.GetFailedCastResultForVital((Vital)innateRequirement);
                            break;
                    }
                }

                for (int i = 0; i < parameters.SpellInfo.Entry.InnateCostTypes.Length; i++)
                {
                    uint innateCostType = parameters.SpellInfo.Entry.InnateCostTypes[i];
                    if (innateCostType == 0)
                        continue;

                    if (caster.GetVitalValue((Vital)innateCostType) < parameters.SpellInfo.Entry.InnateCosts[i])
                        return GlobalSpellManager.Instance.GetFailedCastResultForVital((Vital)innateCostType);
                }
            }

            return CastResult.Ok;
        }

        private CastResult CheckPrerequisites()
        {
            // TODO: Remove below line and evaluate PreReq's for Non-Player Entities
            if (!(caster is Player player))
                return CastResult.Ok;

            if (parameters.SpellInfo.CasterCastPrerequisite != null && !CheckRunnerOverride(player))
            {
                if (!PrerequisiteManager.Instance.Meets(player, parameters.SpellInfo.CasterCastPrerequisite.Id))
                    return CastResult.PrereqCasterCast;
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

            return CastResult.Ok;
        }

        private bool CheckRunnerOverride(Player player)
        {
            foreach (PrerequisiteEntry runnerPrereq in parameters.SpellInfo.PrerequisiteRunners)
                if (PrerequisiteManager.Instance.Meets(player, runnerPrereq.Id))
                    return true;

            return false;
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

        private Spell InitialiseThresholdSpell()
        {
            if (parameters.SpellInfo.Thresholds.Count == 0)
                return null;

            Spell4ThresholdsEntry thresholdsEntry = parameters.SpellInfo.Thresholds.FirstOrDefault(i => i.OrderIndex == thresholdValue);
            if (thresholdsEntry == null)
                throw new InvalidOperationException($"ThresholdsEntry should exist at index {thresholdValue} but doesn't for Spell4ID {Spell4Id}!");

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(thresholdsEntry.Spell4IdToCast);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException();

            SpellBaseInfo spellBaseInfo = GlobalSpellManager.Instance.GetSpellBaseInfo(spell4Entry.Spell4BaseIdBaseSpell);
            if (spellBaseInfo == null)
                throw new ArgumentOutOfRangeException();

            SpellInfo spellInfo = spellBaseInfo.GetSpellInfo((byte)spell4Entry.TierIndex);
            if (spellInfo == null)
                throw new ArgumentOutOfRangeException();

            Spell thresholdSpell = new Spell(caster, new SpellParameters
                {
                    SpellInfo = spellInfo,
                    ParentSpellInfo = parameters.SpellInfo,
                    RootSpellInfo = parameters.SpellInfo,
                    UserInitiatedSpellCast = parameters.UserInitiatedSpellCast,
                    ThresholdValue = thresholdsEntry.OrderIndex + 1
                });

            log.Trace($"Added Child Spell {thresholdSpell.Spell4Id} with casting ID {thresholdSpell.CastingId} to parent casting ID {CastingId}");

            return thresholdSpell;
        }

        private void InitialiseCastMethod()
        {
            CastMethodDelegate handler = GlobalSpellManager.Instance.GetCastMethodHandler(CastMethod);
            if (handler == null)
            {
                log.Warn($"Unhandled cast method {CastMethod}. Using {CastMethod.Normal} instead.");
                GlobalSpellManager.Instance.GetCastMethodHandler(CastMethod.Normal).Invoke(this);
            }
            else
                handler.Invoke(this);
        }

        private void HandleThresholdCast()
        {
            if (status != SpellStatus.Waiting)
                throw new InvalidOperationException();

            if (parameters.SpellInfo.Thresholds.Count == 0)
                throw new InvalidOperationException();   

            CastResult result = CheckCast();
            if (result != CastResult.Ok)
            {
                SendSpellCastResult(result);
                return;
            }

            Spell thresholdSpell = InitialiseThresholdSpell();
            thresholdSpell.Cast();
            thresholdSpells.Add(thresholdSpell);

            switch (CastMethod)
            {
                case CastMethod.ChargeRelease:
                    SetCooldown();
                    thresholdValue = thresholdMax;
                    break;
                case CastMethod.RapidTap:
                    thresholdValue++;
                    break;
            }
        }

        /// <summary>
        /// Cancel cast with supplied <see cref="CastResult"/>.
        /// </summary>
        public void CancelCast(CastResult result)
        {
            if (!IsCasting && !HasThresholdToCast)
                throw new InvalidOperationException();

            if (HasThresholdToCast && thresholdSpells.Count > 0)
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

                if (result == CastResult.CasterMovement)
                    player?.SpellManager.SetGlobalSpellCooldown(0d);

                SendSpellCastResult(result);
            }

            events.CancelEvents();
            status = SpellStatus.Finishing;

            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} cast was cancelled.");
        }

        private void Execute()
        {
            status = SpellStatus.Executing;
            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} has started executing.");

            if ((currentPhase == 0 || currentPhase == 255) && !HasThresholdToCast && CastMethod != CastMethod.ChargeRelease)
            {
                CostSpell();
                SetCooldown();
            }

            SelectTargets();
            ExecuteEffects();
            HandleVisual();

            SendSpellGo();

            // TODO: Confirm whether RapidTap spells cancel another out, and add logic as necessary

            if (parameters.SpellInfo.Entry.ThresholdTime > 0)
                SendThresholdStart();

            if (parameters.ThresholdValue > 0 && parameters.RootSpellInfo.Thresholds.Count > 1)
                SendThresholdUpdate();
        }

        private void SetCooldown()
        {
            if (caster is Player player)
                if (parameters.SpellInfo.Entry.SpellCoolDown != 0u)
                    player.SpellManager.SetSpellCooldown(parameters.SpellInfo.Entry.Id, parameters.SpellInfo.Entry.SpellCoolDown / 1000d);
        }

        private void CostSpell()
        {
            if (parameters.CharacterSpell?.MaxAbilityCharges > 0)
                parameters.CharacterSpell.UseCharge();

            for (int i = 0; i < parameters.SpellInfo.Entry.InnateCostTypes.Length; i++)
            {
                uint innateCostType = parameters.SpellInfo.Entry.InnateCostTypes[i];
                if (innateCostType == 0)
                    continue;

                caster.ModifyVital((Vital)innateCostType, parameters.SpellInfo.Entry.InnateCosts[i] * -1f);
            }
        }

        private void HandleVisual()
        {
            foreach (Spell4VisualEntry visual in parameters.SpellInfo.Visuals)
            {
                VisualEffectEntry visualEffect = GameTableManager.Instance.VisualEffect.GetEntry(visual.VisualEffectId);
                if (visualEffect == null)
                    throw new InvalidOperationException($"VisualEffectEntry with ID {visual.VisualEffectId} does not exist");

                if (visualEffect.VisualType == 0 && visualEffect.ModelSequenceIdTarget00 > 0)
                {
                    ushort emotesId = (ushort)(GameTableManager.Instance.Emotes.Entries.FirstOrDefault(i => i.NoArgAnim == visualEffect.ModelSequenceIdTarget00)?.Id ?? 0u);

                    // TODO: Adjust logic as necessary. It's possible that there are other packets used instead of the ServerEntityEmote to have them "play" effects appropriately.
                    if (emotesId == 0)
                        return;

                    caster.EnqueueToVisible(new ServerEntityEmote
                    {
                        EmotesId = emotesId,
                        SourceUnitId = caster.Guid
                    }, true);
                    
                    if (visualEffect.Duration > 0)
                        events.EnqueueEvent(new SpellEvent(visualEffect.Duration / 1000d, () => 
                        {
                            caster.EnqueueToVisible(new ServerEntityEmote
                            {
                                EmotesId = 0,
                                SourceUnitId = caster.Guid
                            }, true);
                        }));
                }
            }
        }

        private void SelectTargets()
        {
            targets.Clear();

            targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Caster, caster));

            if (parameters.PrimaryTargetId > 0)
            {
                UnitEntity primaryTargetEntity = caster.GetVisible<UnitEntity>(parameters.PrimaryTargetId);
                if (primaryTargetEntity != null)
                    targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Target, primaryTargetEntity));
            }

            foreach (TelegraphDamageEntry telegraphDamageEntry in parameters.SpellInfo.Telegraphs)
            {
                Telegraph telegraph = null;
                if (parameters.PrimaryTargetId > 0)
                {
                    UnitEntity primaryTargetEntity = caster.GetVisible<UnitEntity>(parameters.PrimaryTargetId);
                    if (primaryTargetEntity != null)
                        telegraph = new Telegraph(telegraphDamageEntry, caster, primaryTargetEntity.Position, primaryTargetEntity.Rotation);
                }
                else
                    telegraph = new Telegraph(telegraphDamageEntry, caster, caster.Position, caster.Rotation);

                foreach (UnitEntity entity in telegraph.GetTargets())
                    targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Telegraph, entity));
            }
        }

        private void ExecuteEffects()
        {
            // Using For..Loop instead of foreach intentionally, as this can be modified as effects are evaluated.
            for (int index = 0; index < parameters.SpellInfo.Effects.Count(); index++)
            {
                Spell4EffectsEntry spell4EffectsEntry = parameters.SpellInfo.Effects[index];

                if (caster is Player player)
                {
                    // Ensure caster can apply this effect
                    if (spell4EffectsEntry.PrerequisiteIdCasterApply > 0 && !PrerequisiteManager.Instance.Meets(player, spell4EffectsEntry.PrerequisiteIdCasterApply))
                        continue;
                }

                if (CastMethod == CastMethod.Multiphase && currentPhase < 255)
                {
                    int phaseMask = 1 << currentPhase;
                    if (spell4EffectsEntry.PhaseFlags != 1 && (phaseMask & spell4EffectsEntry.PhaseFlags) == 0)
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
                        if (!CheckEffectApplyPrerequisites(spell4EffectsEntry, effectTarget.Entity, effectTarget.Flags))
                            continue;

                        var info = new SpellTargetInfo.SpellTargetEffectInfo(effectId, spell4EffectsEntry);
                        effectTarget.Effects.Add(info);

                        // TODO: if there is an unhandled exception in the handler, there will be an infinite loop on Execute()
                        events.EnqueueEvent(new SpellEvent(spell4EffectsEntry.DelayTime / 1000d, () =>
                        {
                            handler.Invoke(this, effectTarget.Entity, info);
                        }));
                    }

                    // Add durations for each effect so that when the Effect timer runs out, the Spell can Finish.
                    if (spell4EffectsEntry.DurationTime > 0)
                        events.EnqueueEvent(new SpellEvent(spell4EffectsEntry.DurationTime / 1000d, () => { /* placeholder for duration */ }));

                    if (spell4EffectsEntry.DurationTime > 0 && spell4EffectsEntry.DurationTime > duration)
                        duration = spell4EffectsEntry.DurationTime;

                    if (spell4EffectsEntry.DurationTime == 0u && ((SpellEffectFlags)spell4EffectsEntry.Flags & SpellEffectFlags.CancelOnly) != 0)
                        parameters.ForceCancelOnly = true;
                }
            }
        }

        private bool CheckEffectApplyPrerequisites(Spell4EffectsEntry spell4EffectsEntry, UnitEntity unit, SpellEffectTargetFlags targetFlags)
        {
            // TODO: Possibly update Prereq Manager to handle other Units
            if (!(unit is Player player))
                return true;

            if ((targetFlags & SpellEffectTargetFlags.Caster) != 0)
            {
                // TODO
                if (spell4EffectsEntry.PrerequisiteIdCasterApply > 0)
                {
                    return PrerequisiteManager.Instance.Meets(player, spell4EffectsEntry.PrerequisiteIdCasterApply);
                }
            }

            if ((targetFlags & SpellEffectTargetFlags.Caster) == 0)
            {
                if (spell4EffectsEntry.PrerequisiteIdTargetApply > 0)
                {
                    return PrerequisiteManager.Instance.Meets(player, spell4EffectsEntry.PrerequisiteIdTargetApply);
                }
            }

            return true;
        }

        public bool IsMovingInterrupted()
        {
            // TODO: implement correctly
            return parameters.UserInitiatedSpellCast && parameters.SpellInfo.BaseInfo.SpellType.Id != 5 && parameters.SpellInfo.Entry.CastTime > 0;
        }

        /// <summary>
        /// Finish this <see cref="Spell"/> and end all effects associated with it.
        /// </summary>
        public void Finish()
        {
            if (status == SpellStatus.Finished)
                return;

            thresholdValue = thresholdMax;
            events.CancelEvents();
            caster.RemoveEffect(CastingId);
            status = SpellStatus.Finishing;
        }

        /// <summary>
        /// Used when a <see cref="CSI.ClientSideInteraction"/> succeeds
        /// </summary>
        public void SucceedClientInteraction()
        {
            if (parameters.ClientSideInteraction == null)
                throw new ArgumentException("This can only be used by a Client Interaction Event.");

            Execute();
        }

        /// <summary>
        /// Used when a <see cref="CSI.ClientSideInteraction"/> fails
        /// </summary>
        public void FailClientInteraction()
        {
            if (parameters.ClientSideInteraction == null)
                throw new ArgumentException("This can only be used by a Client Interaction Event.");

            parameters.ClientSideInteraction.TriggerFail();

            CancelCast(CastResult.ClientSideInteractionFail);
        }

        private void SendSpellCastResult(CastResult castResult)
        {
            if (castResult == CastResult.Ok)
                return;

            log.Trace($"Spell {parameters.SpellInfo.Entry.Id} failed to cast {castResult}.");

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
            ServerSpellStart spellStart = new ServerSpellStart
            {
                CastingId            = CastingId,
                CasterId             = caster.Guid,
                PrimaryTargetId      = parameters.PrimaryTargetId > 0 ? parameters.PrimaryTargetId : caster.Guid,
                Spell4Id             = parameters.SpellInfo.Entry.Id,
                RootSpell4Id         = parameters.RootSpellInfo?.Entry.Id ?? 0,
                ParentSpell4Id       = parameters.ParentSpellInfo?.Entry.Id ?? 0,
                FieldPosition        = new Position(caster.Position),
                Yaw                  = caster.Rotation.X,
                UserInitiatedSpellCast = parameters.UserInitiatedSpellCast,
                InitialPositionData  = new List<InitialPosition>(),
                TelegraphPositionData = new List<TelegraphPosition>()
            };

            List<UnitEntity> unitsCasting = new List<UnitEntity>();
            if (parameters.PrimaryTargetId > 0)
                unitsCasting.Add(caster.GetVisible<UnitEntity>(parameters.PrimaryTargetId));
            else
                unitsCasting.Add(caster);

            foreach (UnitEntity unit in unitsCasting)
                spellStart.InitialPositionData.Add(new InitialPosition
                {
                    UnitId = unit.Guid,
                    Position = new Position(unit.Position),
                    TargetFlags = 3,
                    Yaw = unit.Rotation.X
                });

            foreach (UnitEntity unit in unitsCasting)
                foreach (TelegraphDamageEntry telegraph in parameters.SpellInfo.Telegraphs)
                    spellStart.TelegraphPositionData.Add(new TelegraphPosition
                    {
                        TelegraphId = (ushort)telegraph.Id,
                        AttachedUnitId = unit.Guid,
                        TargetFlags = (byte)telegraph.TargetTypeFlags,
                        Position = new Position(unit.Position),
                        Yaw = unit.Rotation.X
                    });


            caster.EnqueueToVisible(spellStart, true);
        }

        private void SendSpellStartClientInteraction()
        {
            // Shoule we actually emit client interaction events to everyone? - Logs suggest that we only see this packet firing when the client interacts with -something- and is likely only sent to them
            if(caster is Player player)
            {
                player.Session.EnqueueMessageEncrypted(new ServerSpellStartClientInteraction
                {
                    ClientUniqueId = parameters.ClientSideInteraction.ClientUniqueId,
                    CastingId = CastingId,
                    CasterId = parameters.PrimaryTargetId
                });
            }
            
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
                Phase              = currentPhase
            };

            byte targetCount = 0;
            foreach (SpellTargetInfo targetInfo in targets
                .Where(t => t.Effects.Count > 0))
            {
                var networkTargetInfo = new TargetInfo
                {
                    UnitId        = targetInfo.Entity.Guid,
                    Ndx           = targetCount++,
                    TargetFlags   = (byte)targetInfo.Flags,
                    InstanceCount = 1,
                    CombatResult  = CombatResult.Hit
                };

                foreach (SpellTargetInfo.SpellTargetEffectInfo targetEffectInfo in targetInfo.Effects)
                {
                    var networkTargetEffectInfo = new TargetInfo.EffectInfo
                    {
                        Spell4EffectId = targetEffectInfo.Entry.Id,
                        EffectUniqueId = targetEffectInfo.EffectId,
                        DelayTime      = targetEffectInfo.Entry.DelayTime,
                        TimeRemaining  = duration > 0 ? (int)duration : -1
                    };

                    if (targetEffectInfo.Damage != null)
                    {
                        networkTargetEffectInfo.InfoType = 1;
                        networkTargetEffectInfo.DamageDescriptionData = new TargetInfo.EffectInfo.DamageDescription
                        {
                            RawDamage          = targetEffectInfo.Damage.RawDamage,
                            RawScaledDamage    = targetEffectInfo.Damage.RawScaledDamage,
                            AbsorbedAmount     = targetEffectInfo.Damage.AbsorbedAmount,
                            ShieldAbsorbAmount = targetEffectInfo.Damage.ShieldAbsorbAmount,
                            AdjustedDamage     = targetEffectInfo.Damage.AdjustedDamage,
                            OverkillAmount     = targetEffectInfo.Damage.OverkillAmount,
                            KilledTarget       = targetEffectInfo.Damage.KilledTarget,
                            CombatResult       = CombatResult.Hit,
                            DamageType         = targetEffectInfo.Damage.DamageType
                        };
                    }

                    networkTargetInfo.EffectInfoData.Add(networkTargetEffectInfo);
                }

                serverSpellGo.TargetInfoData.Add(networkTargetInfo);
            }

            List<UnitEntity> unitsCasting = new List<UnitEntity>
                {
                    caster
                };

            foreach (UnitEntity unit in unitsCasting)
                serverSpellGo.InitialPositionData.Add(new Network.Message.Model.Shared.InitialPosition
                {
                    UnitId = unit.Guid,
                    Position = new Position(unit.Position),
                    TargetFlags = 3,
                    Yaw = unit.Rotation.X
                });

            foreach (UnitEntity unit in unitsCasting)
                foreach (TelegraphDamageEntry telegraph in parameters.SpellInfo.Telegraphs)
                    serverSpellGo.TelegraphPositionData.Add(new Network.Message.Model.Shared.TelegraphPosition
                    {
                        TelegraphId = (ushort)telegraph.Id,
                        AttachedUnitId = unit.Guid,
                        TargetFlags = 3,
                        Position = new Position(unit.Position),
                        Yaw = unit.Rotation.X
                    });

            caster.EnqueueToVisible(serverSpellGo, true);
        }

        private void SendRemoveBuff(uint unitId)
        {
            if (!parameters.SpellInfo.BaseInfo.HasIcon)
                throw new InvalidOperationException();

            caster.EnqueueToVisible(new ServerSpellBuffRemove
            {
                CastingId = CastingId,
                CasterId  = unitId
            }, true);
        }
        
        private void SendThresholdStart()
        {
            if (caster is Player player)
                player.Session.EnqueueMessageEncrypted(new ServerSpellThresholdStart
                {
                    Spell4Id = parameters.SpellInfo.Entry.Id,
                    RootSpell4Id = parameters.RootSpellInfo?.Entry.Id ?? 0,
                    ParentSpell4Id = parameters.ParentSpellInfo?.Entry.Id ?? 0,
                    CastingId = CastingId
                });
        }

        public void SendThresholdUpdate()
        {
            if (caster is Player player)
                player.Session.EnqueueMessageEncrypted(new ServerSpellThresholdUpdate
                {
                    Spell4Id = parameters.ParentSpellInfo?.Entry.Id ?? Spell4Id,
                    Value = parameters.ThresholdValue > 0 ? (byte)parameters.ThresholdValue : (byte)thresholdValue
                });
        }

        public void SendBuffRemoved()
        {
            if (targets.Count == 0)
                return;

            Server0811 spellTargets = new Server0811
            {
                CastingId = CastingId,
                SpellTargets = targets.Select(i => i.Entity.Guid).ToList()
            };

            caster.EnqueueToVisible(spellTargets, true);
        }
    }
}
