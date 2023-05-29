using System;
using System.Linq;
using System.Numerics;
using NexusForever.Shared;
using NexusForever.Shared.Game.Events;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.Shared.Network;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Housing;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Game.Prerequisite;
using NexusForever.WorldServer.Game.Spell.Event;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Game.Spell
{
    public delegate void SpellEffectDelegate(Spell spell, UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info);

    public partial class Spell
    {
        private void TickingEvent(double tickTime, Action action)
        {
            events.EnqueueEvent(new SpellEvent(tickTime / 1000d, () =>
            {
                action.Invoke();
                TickingEvent(tickTime, action);
            }));
        }

        private uint CalculateDamageOrHealingFromParameters(SpellTargetInfo.SpellTargetEffectInfo info)
        {
            uint damage = 0;
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType00, info.Entry.ParameterValue00);
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType01, info.Entry.ParameterValue01);
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType02, info.Entry.ParameterValue02);
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType03, info.Entry.ParameterValue03);
            return damage;
        }

        [SpellEffectHandler(SpellEffectType.Damage)]
        private void HandleEffectDamage(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!target.CanAttack(caster))
                return;

            void DealDamage(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
            {
                uint damage = CalculateDamageOrHealingFromParameters(info);

                DamageCalculator.Instance.CalculateDamage(caster, target, this, info, (DamageType)info.Entry.DamageType, damage);

                if (info.Damage.CombatResult == CombatResult.Critical)
                    caster.FireProc(ProcType.CriticalDamage);

                target.TakeDamage(caster, info.Damage, info.Entry.ThreatMultiplier);
            }

            if (info.Entry.TickTime > 0)
            {
                double tickTime = info.Entry.TickTime;
                if (info.Entry.DurationTime > 0)
                {
                    for (int i = 1; i <= info.Entry.DurationTime / tickTime; i++)
                        events.EnqueueEvent(new SpellEvent(tickTime * i / 1000d, () =>
                        {
                            target.EnqueueToVisible(new ServerSpellGoEffect
                            {
                                ServerUniqueId = CastingId,
                                Spell4EffectId = info.Entry.Id,
                                TargetId = target.Guid,
                                DamageDescriptionData = {
                                    new Network.Message.Model.Shared.DamageDescription
                                    {
                                        ShieldAbsorbAmount = info.Damage.ShieldAbsorbAmount,
                                        RawScaledDamage = info.Damage.RawScaledDamage,
                                        AbsorbedAmount = info.Damage.AbsorbedAmount,
                                        AdjustedDamage = info.Damage.AdjustedDamage,
                                        CombatResult = info.Damage.CombatResult,
                                        DamageType = info.Damage.DamageType,
                                        KilledTarget = info.Damage.KilledTarget,
                                        OverkillAmount = info.Damage.OverkillAmount,
                                        RawDamage = info.Damage.RawDamage
                                    }
                                }
                            }, true);
                            DealDamage(target, info);
                        }));
                }
                else
                    TickingEvent(tickTime, () =>
                    {
                        target.EnqueueToVisible(new ServerSpellGoEffect
                        {
                            ServerUniqueId = CastingId,
                            Spell4EffectId = info.Entry.Id,
                            TargetId = target.Guid,
                            DamageDescriptionData = {
                                    new Network.Message.Model.Shared.DamageDescription
                                    {
                                        ShieldAbsorbAmount = info.Damage.ShieldAbsorbAmount,
                                        RawScaledDamage = info.Damage.RawScaledDamage,
                                        AbsorbedAmount = info.Damage.AbsorbedAmount,
                                        AdjustedDamage = info.Damage.AdjustedDamage,
                                        CombatResult = info.Damage.CombatResult,
                                        DamageType = info.Damage.DamageType,
                                        KilledTarget = info.Damage.KilledTarget,
                                        OverkillAmount = info.Damage.OverkillAmount,
                                        RawDamage = info.Damage.RawDamage
                                    }
                                }
                        }, true);
                        DealDamage(target, info);
                    });
            }
            else
                DealDamage(target, info);
        }

        [SpellEffectHandler(SpellEffectType.Heal)]
        private void HandleEffectHeal(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (target.CanAttack(caster))
                return;

            uint healing = CalculateDamageOrHealingFromParameters(info);
            info.AddDamage(new SpellTargetInfo.SpellTargetEffectInfo.DamageDescription
            {
                CombatResult = CombatResult.Hit,
                DamageType = DamageType.Heal,
                RawDamage = healing,
                AdjustedDamage = healing
            });
            target.ModifyHealth(healing);
        }

        [SpellEffectHandler(SpellEffectType.UnitPropertyModifier)]
        private void HandleEffectPropertyModifier(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            // TODO: Handle NPCs and other Entities.

            if (!(target is Player player))
                return;

            PropertyModifier modifier = new PropertyModifier(info.Entry.DataBits01, BitConverter.Int32BitsToSingle((int)info.Entry.DataBits02), BitConverter.Int32BitsToSingle((int)info.Entry.DataBits03));
            player.AddSpellModifierProperty((Property)info.Entry.DataBits00, parameters.SpellInfo.Entry.Id, modifier);

            if (info.Entry.DurationTime > 0d)
                events.EnqueueEvent(new SpellEvent(info.Entry.DurationTime / 1000d, () =>
                {
                    player.RemoveSpellProperty((Property)info.Entry.DataBits00, parameters.SpellInfo.Entry.Id);
                }));
        }

        [SpellEffectHandler(SpellEffectType.Proxy)]
        private void HandleEffectProxy(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (effectTriggerCount.TryGetValue(info.Entry.Id, out uint count))
                if (count >= info.Entry.DataBits04)
                    return;

            proxies.Add(new Proxy(target, info.Entry, this, parameters));
        }

        [SpellEffectHandler(SpellEffectType.Disguise)]
        private void HandleEffectDisguise(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            Creature2Entry creature2 = GameTableManager.Instance.Creature2.GetEntry(info.Entry.DataBits02);
            if (creature2 == null)
                return;

            Creature2DisplayGroupEntryEntry displayGroupEntry = GameTableManager.Instance.Creature2DisplayGroupEntry.Entries.FirstOrDefault(d => d.Creature2DisplayGroupId == creature2.Creature2DisplayGroupId);
            if (displayGroupEntry == null)
                return;

            player.SetDisplayInfo(displayGroupEntry.Creature2DisplayInfoId);
        }

        [SpellEffectHandler(SpellEffectType.SummonMount)]
        private void HandleEffectSummonMount(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            // TODO: handle NPC mounting?
            if (!(target is Player player))
                return;

            if (!player.CanMount())
                return;

            var mount = new Mount(player, parameters.SpellInfo.Entry.Id, info.Entry.DataBits00, info.Entry.DataBits01, info.Entry.DataBits04);
            mount.EnqueuePassengerAdd(player, VehicleSeatType.Pilot, 0);

            var position = new MapPosition
            {
                Position = player.Position
            };

            if (player.Map.CanEnter(mount, position))
                player.Map.EnqueueAdd(mount, position);

            // FIXME: also cast 52539,Riding License - Riding Skill 1 - SWC - Tier 1,34464 -- upon further investigation, this appeared to only trigger for characters who were created earlier in the game's lifetime.
            // Expert - 52543

            // TODO: There are other Riding Skills which need to be added when the player has them as known effects.
            player.CastSpell(52539, new SpellParameters
            {
                ParentSpellInfo        = parameters.SpellInfo,
                RootSpellInfo          = parameters.RootSpellInfo,
                UserInitiatedSpellCast = false,
                IsProxy                = true
            });
        }


        [SpellEffectHandler(SpellEffectType.SummonCreature)]
        private void HandleEffectSummonCreature(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            // TODO: Investigate flags in dataBits01

            var creatureEntry = GameTableManager.Instance.Creature2.GetEntry(info.Entry.DataBits00);

            for (int i = 0; i < info.Entry.DataBits02; i++)
            {
                WorldEntity entity;
                if ((EntityType)creatureEntry.CreationTypeEnum == EntityType.Simple)
                    entity = new Simple(creatureEntry, 0, 0);
                else if ((EntityType)creatureEntry.CreationTypeEnum == EntityType.NonPlayer)
                    entity = new NonPlayer(creatureEntry, 0, 0);
                else
                    entity = EntityManager.Instance.NewEntity((EntityType)creatureEntry.CreationTypeEnum) ?? EntityManager.Instance.NewEntity(EntityType.Simple);
                entity.Initialise(creatureEntry);

                var position = new MapPosition
                {
                    Position = new Vector3(target.Position.X, target.Position.Y, target.Position.Z).GetRandomPoint2DRangeDirection(info.Entry.DataBits03, info.Entry.DataBits04, info.Entry.DataBits05, target.Rotation.X)
                };

                if (target.Map.CanEnter(entity, position))
                    target.Map.EnqueueAdd(entity, position);
            }
        }

        [SpellEffectHandler(SpellEffectType.Teleport)]
        private void HandleEffectTeleport(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            // Handle NPC teleporting?

            if (!(target is Player player))
                return;

            // Assuming that this is Recall to Transmat
            if (info.Entry.DataBits00 == 0)
            {
                if (player.BindPoint == 0) // Must have bindpoint set
                    return;

                Location bindPointLocation = AssetManager.Instance.GetBindPoint(player.BindPoint);
                Vector3 offset = new Vector3(2f, 1.5f, 2f); // TODO: Should use new Vector3(0f, 1.5f, 0f); when map props are being used

                if (player.CanTeleport()) {
                    player.Rotation = bindPointLocation.Rotation;
                    player.TeleportTo(bindPointLocation.World, Vector3.Add(bindPointLocation.Position, offset));
                }
                return;
            }

            WorldLocation2Entry locationEntry = GameTableManager.Instance.WorldLocation2.GetEntry(info.Entry.DataBits00);
            if (locationEntry == null)
                return;

            // Handle Housing Teleport
            if (locationEntry.WorldId == 1229)
            {
                Residence residence = GlobalResidenceManager.Instance.GetResidenceByOwner(player.Name);
                if (residence == null)
                    residence = GlobalResidenceManager.Instance.CreateResidence(player);

                ResidenceEntrance entrance = GlobalResidenceManager.Instance.GetResidenceEntrance(residence.PropertyInfoId);
                if (player.CanTeleport())
                {
                    player.Rotation = entrance.Rotation.ToEulerDegrees();
                    player.TeleportTo(entrance.Entry, entrance.Position, residence.Parent?.Id ?? residence.Id);
                    return;
                }
            }

            if (player.CanTeleport()) {
                player.Rotation = new Quaternion(locationEntry.Facing0, locationEntry.Facing1, locationEntry.Facing2, locationEntry.Facing3).ToEulerDegrees();
                player.TeleportTo((ushort)locationEntry.WorldId, locationEntry.Position0, locationEntry.Position1, locationEntry.Position2);
            }
        }

        [SpellEffectHandler(SpellEffectType.FullScreenEffect)]
        private void HandleFullScreenEffect(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            // TODO/FIXME: Add duration into the queue so that the spell will automatically finish at the correct time. This is a workaround for Full Screen Effects.
            events.EnqueueEvent(new Event.SpellEvent(info.Entry.DurationTime / 1000d, () => { status = SpellStatus.Finished; SendSpellFinish(); }));
        }

        [SpellEffectHandler(SpellEffectType.RapidTransport)]
        private void HandleEffectRapidTransport(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            TaxiNodeEntry taxiNode = GameTableManager.Instance.TaxiNode.GetEntry(parameters.TaxiNode);
            if (taxiNode == null)
                return;

            WorldLocation2Entry worldLocation = GameTableManager.Instance.WorldLocation2.GetEntry(taxiNode.WorldLocation2Id);
            if (worldLocation == null)
                return;

            if (!(target is Player player))
                return;

            if (!player.CanTeleport())
                return;

            var rotation = new Quaternion(worldLocation.Facing0, worldLocation.Facing0, worldLocation.Facing2, worldLocation.Facing3);
            player.Rotation = rotation.ToEulerDegrees();
            player.TeleportTo((ushort)worldLocation.WorldId, worldLocation.Position0, worldLocation.Position1, worldLocation.Position2);
        }

        [SpellEffectHandler(SpellEffectType.LearnDyeColor)]
        private void HandleEffectLearnDyeColor(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            player.Session.GenericUnlockManager.Unlock((ushort)info.Entry.DataBits00);
        }

        [SpellEffectHandler(SpellEffectType.UnlockMount)]
        private void HandleEffectUnlockMount(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(info.Entry.DataBits00);
            player.SpellManager.AddSpell(spell4Entry.Spell4BaseIdBaseSpell);

            player.Session.EnqueueMessageEncrypted(new ServerUnlockMount
            {
                Spell4Id = info.Entry.DataBits00
            });
        }

        [SpellEffectHandler(SpellEffectType.UnlockPetFlair)]
        private void HandleEffectUnlockPetFlair(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            player.PetCustomisationManager.UnlockFlair((ushort)info.Entry.DataBits00);
        }

        [SpellEffectHandler(SpellEffectType.UnlockVanityPet)]
        private void HandleEffectUnlockVanityPet(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(info.Entry.DataBits00);
            player.SpellManager.AddSpell(spell4Entry.Spell4BaseIdBaseSpell);

            player.Session.EnqueueMessageEncrypted(new ServerUnlockMount
            {
                Spell4Id = info.Entry.DataBits00
            });
        }

        [SpellEffectHandler(SpellEffectType.SummonVanityPet)]
        private void HandleEffectSummonVanityPet(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            player.PetManager.SummonPet(PetType.VanityPet, info.Entry.DataBits00, CastingId, parameters.SpellInfo.Entry, info.Entry);
        }

        [SpellEffectHandler(SpellEffectType.TitleGrant)]
        private void HandleEffectTitleGrant(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            player.TitleManager.AddTitle((ushort)info.Entry.DataBits00);
        }

        [SpellEffectHandler(SpellEffectType.Fluff)]
        private void HandleEffectFluff(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
        }

        [SpellEffectHandler(SpellEffectType.Stealth)]
        private void HandleEffectStealth(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            // TODO: Make it so that Stealth cannot be broken by damage after 3s.
            // This is referenced by EffectId 95774. It checks a Prerequisite that you have http://www.jabbithole.com/spells/assassin-59389. If you do, it'll trigger this EffectHandler with DataBits02 set to 1 (instead of 0).
            if (info.Entry.DataBits02 == 1)
                return;

            target.AddStatus(CastingId, EntityStatus.Stealth);
        }

        [SpellEffectHandler(SpellEffectType.ModifySpellCooldown)]
        private void HandleEffectModifySpellCooldown(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            switch ((EffectModifySpellCooldownType)info.Entry.DataBits00)
            {
                case EffectModifySpellCooldownType.SpellBase:
                    player.SpellManager.SetSpellCooldownByBaseSpell(info.Entry.DataBits01, info.Entry.DataBits02, BitConverter.Int32BitsToSingle((int)info.Entry.DataBits03));
                    log.Warn($"Setting SpellBase CD: {BitConverter.Int32BitsToSingle((int)info.Entry.DataBits03)}");
                    break;
                case EffectModifySpellCooldownType.Spell4:
                    player.SpellManager.SetSpellCooldown(info.Entry.DataBits01, BitConverter.Int32BitsToSingle((int)info.Entry.DataBits02));
                    break;
                case EffectModifySpellCooldownType.SpellGroupId:
                    player.SpellManager.SetSpellCooldownByGroupId(info.Entry.DataBits01, BitConverter.Int32BitsToSingle((int)info.Entry.DataBits02));
                    break;
                case EffectModifySpellCooldownType.SpellCooldownId:
                    player.SpellManager.SetSpellCooldownByCooldownId(info.Entry.DataBits01, BitConverter.Int32BitsToSingle((int)info.Entry.DataBits02));
                    break;
                default:
                    log.Warn($"Unhandled ModifySpellCooldown Type {(EffectModifySpellCooldownType)info.Entry.DataBits00}");
                    break;
            }
        }

        [SpellEffectHandler(SpellEffectType.SpellForceRemove)]
        private void HandleEffectSpellForceRemove(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            switch ((EffectForceSpellRemoveType)info.Entry.DataBits00)
            {
                case EffectForceSpellRemoveType.SpellGroupId:
                    target.FinishSpellsByGroup(info.Entry.DataBits01);
                    break;
                case EffectForceSpellRemoveType.Spell4:
                    target.FinishSpells(info.Entry.DataBits01);
                    break;
                case EffectForceSpellRemoveType.SpellBase:
                    target.FinishSpells(info.Entry.DataBits01);
                    break;
                default:
                    log.Warn($"Unhandled EffectForceSpellRemoveType Type {(EffectForceSpellRemoveType)info.Entry.DataBits00}");
                    break;
            }
        }

        [SpellEffectHandler(SpellEffectType.RavelSignal)]
        private void HandleEffectRavelSignal(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (info.Entry.DataBits00 == 1 && info.Entry.DataBits01 == 13076) // TODO: Move to actual script system. This is used in Stalker's Stealth Ability to prevent it from executing the next Effect whcih was the Cancel Stealth proxy effect.
                parameters.ParentSpellInfo.Effects.RemoveAll(i => i.Id == 91018);
            else
                log.Warn($"Unhandled spell effect {SpellEffectType.RavelSignal}");
        }

        [SpellEffectHandler(SpellEffectType.Activate)]
        private void HandleEffectActivate(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (parameters.ClientSideInteraction == null)
                log.Error($"No CSI present for spell {Spell4Id} cast by {caster.Type}");

            parameters.ClientSideInteraction?.HandleSuccess(parameters);
        }

        [SpellEffectHandler(SpellEffectType.ForcedMove)]
        private void HandleEffectForcedMove(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
        }
        
        [SpellEffectHandler(SpellEffectType.VitalModifier)]
        private void HandleEffectVitalModifier(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            Vital vital = (Vital)info.Entry.DataBits00;
            float amount = info.Entry.DataBits01 > int.MaxValue ? -(uint.MaxValue - info.Entry.DataBits01 + 1) : info.Entry.DataBits01;
            if (info.Entry.TickTime > 0)
            {
                events.EnqueueEvent(new SpellEvent(info.Entry.DelayTime / 1000d, () =>
                {
                    double tickTime = info.Entry.TickTime;
                    if (info.Entry.DurationTime > 0)
                    {
                        for (int i = 1; i >= info.Entry.DurationTime / tickTime; i++)
                            events.EnqueueEvent(new SpellEvent(tickTime * i / 1000d, () =>
                            {
                                if (caster is Player casterPlayer)
                                    if (info.Entry.PrerequisiteIdCasterApply > 0 && !PrerequisiteManager.Instance.Meets(casterPlayer, info.Entry.PrerequisiteIdCasterApply))
                                        return;

                                if (target is Player targetPlayer)
                                    if (info.Entry.PrerequisiteIdTargetApply > 0 && !PrerequisiteManager.Instance.Meets(targetPlayer, info.Entry.PrerequisiteIdTargetApply))
                                        return;

                                target.ModifyVital(vital, amount);
                            }));
                    }
                    else
                        TickingEvent(tickTime, () =>
                        {
                            if (caster is Player casterPlayer)
                                if (info.Entry.PrerequisiteIdCasterApply > 0 && !PrerequisiteManager.Instance.Meets(casterPlayer, info.Entry.PrerequisiteIdCasterApply))
                                    return;

                            if (target is Player targetPlayer)
                                if (info.Entry.PrerequisiteIdTargetApply > 0 && !PrerequisiteManager.Instance.Meets(targetPlayer, info.Entry.PrerequisiteIdTargetApply))
                                    return;

                            target.ModifyVital(vital, amount);
                        });
                }));
            }
            else
                target.ModifyVital(vital, info.Entry.DataBits01);
        }

        [SpellEffectHandler(SpellEffectType.NpcExecutionDelay)]
        private void HandleEffectNpcExecutionDelay(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (target is Player)
                throw new NotImplementedException($"Can only apply execution delay to non-Players");

            target.GetAI()?.AddExecutionDelay(info.Entry.DurationTime);
        }

        [SpellEffectHandler(SpellEffectType.Proc)]
        private void HandleEffectProc(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            ProcInfo procInfo = target.ApplyProc(info.Entry);
            if (procInfo != null)
                log.Trace($"Applied Proc {info.Entry.Id} for {procInfo.Type} to Entity {target.Guid}.");
            else
                log.Trace($"Failed to apply Proc {info.Entry.Id} for {(ProcType)info.Entry.DataBits00} to Entity {target.Guid}.");
        }

        [SpellEffectHandler(SpellEffectType.QuestAdvanceObjective)]
        private void HandleEffectQuestAdvanceObjective(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            player.QuestManager.QuestAchieveObjective((ushort)info.Entry.DataBits00, (byte)info.Entry.DataBits01);
        }

        [SpellEffectHandler(SpellEffectType.SummonPet)]
        private void HandleEffectSummonPet(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            player.PetManager.SummonPet(PetType.CombatPet, info.Entry.DataBits00, CastingId, parameters.SpellInfo.Entry, info.Entry);

            //parameters.IsUnlimitedDuration = true;
        }

        [SpellEffectHandler(SpellEffectType.DisguiseOutfit)]
        private void HandleEffectDisguiseOutfit(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (info.Entry.DataBits00 == 0 && info.Entry.DataBits01 == 0)
            {
                if (info.Entry.DataBits02 > 0)
                {
                    ItemDisplayEntry entry = GameTableManager.Instance.ItemDisplay.GetEntry(info.Entry.DataBits02);
                    target.AddTemporaryDisplayItem(Spell4Id, entry);
                }
            }
        }

        [SpellEffectHandler(SpellEffectType.HousingTeleport)]
        private void HandleEffectHousingTeleport(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            Residence residence = GlobalResidenceManager.Instance.GetResidenceByOwner(player.Name);
            if (residence == null)
                residence = GlobalResidenceManager.Instance.CreateResidence(player);

            ResidenceEntrance entrance = GlobalResidenceManager.Instance.GetResidenceEntrance(residence.PropertyInfoId);
            player.Rotation = entrance.Rotation.ToEulerDegrees();
            player.TeleportTo(entrance.Entry, entrance.Position, residence.Parent?.Id ?? residence.Id);
        }

        [SpellEffectHandler(SpellEffectType.HousingEscape)]
        private void HandleEffectHousingEscape(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (!(target is Player player))
                return;

            if (player.Map == null || !(player.Map is ResidenceMapInstance residenceMap))
                return;

            Residence residence = residenceMap.GetMainResidence();
            if (residence == null)
                return;

            ResidenceEntrance entrance = GlobalResidenceManager.Instance.GetResidenceEntrance(residence.PropertyInfoId);
            player.Rotation = entrance.Rotation.ToEulerDegrees();
            player.TeleportTo(entrance.Entry, entrance.Position, residence.Parent?.Id ?? residence.Id);
        }
    }
}
