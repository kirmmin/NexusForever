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
using NexusForever.WorldServer.Game.Spell.Event;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Game.Spell
{
    public delegate void SpellEffectDelegate(Spell spell, UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info);

    public partial class Spell
    {
        [SpellEffectHandler(SpellEffectType.Damage)]
        private void HandleEffectDamage(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            uint damage = 0;
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType00, info.Entry.ParameterValue00);
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType01, info.Entry.ParameterValue01);
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType02, info.Entry.ParameterValue02);
            damage += DamageCalculator.Instance.GetBaseDamageForSpell(caster, info.Entry.ParameterType03, info.Entry.ParameterValue03);

            DamageCalculator.Instance.CalculateDamage(caster, target, this, ref info, (DamageType)info.Entry.DamageType, damage);
            // TODO: Deal damage
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
            void TickingProxyEvent(double tickTime, Action action)
            {
                events.EnqueueEvent(new SpellEvent(tickTime / 1000d, () =>
                {
                    action.Invoke();
                    TickingProxyEvent(tickTime, action);
                }));
            }

            SpellParameters proxyParameters = new SpellParameters
            {
                ParentSpellInfo = parameters.SpellInfo,
                RootSpellInfo = parameters.RootSpellInfo,
                PrimaryTargetId = target.Guid,
                UserInitiatedSpellCast = parameters.UserInitiatedSpellCast,
                IsProxy = true
            };

            events.EnqueueEvent(new SpellEvent(info.Entry.DelayTime / 1000d, () =>
            {
                if (info.Entry.TickTime > 0)
                {
                    double tickTime = info.Entry.TickTime;
                    if (info.Entry.DurationTime > 0)
                    {
                        for (int i = 1; i == info.Entry.DurationTime / tickTime; i++)
                            events.EnqueueEvent(new SpellEvent(tickTime * i / 1000d, () =>
                            {
                                caster.CastSpell(info.Entry.DataBits01, proxyParameters);
                            }));
                    }
                    else
                        TickingProxyEvent(tickTime, () =>
                        {
                            caster.CastSpell(info.Entry.DataBits01, proxyParameters);
                        });
                }
                else
                    caster.CastSpell(info.Entry.DataBits00, proxyParameters);
            }));
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

            // usually for hover boards
            /*if (info.Entry.DataBits04 > 0u)
            {
                mount.SetAppearance(new ItemVisual
                {
                    Slot      = ItemSlot.Mount,
                    DisplayId = (ushort)info.Entry.DataBits04
                });
            }*/

            player.Map.EnqueueAdd(mount, player.Position);

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

            uint mountSpeedSpell4Id = 0;
            switch (mount.MountType)
            {
                case PetType.GroundMount: // Cast 80530, Mount Sprint  - Tier 2, 36122
                    mountSpeedSpell4Id = 80530;
                    break;
                case PetType.HoverBoard: // Cast 80531, Hoverboard Sprint  - Tier 2, 36122
                    mountSpeedSpell4Id = 80531;
                    break;
                default:
                    mountSpeedSpell4Id = 80530;
                    break;

            }
            player.CastSpell(mountSpeedSpell4Id, new SpellParameters
            {
                ParentSpellInfo        = parameters.SpellInfo,
                RootSpellInfo          = parameters.RootSpellInfo,
                UserInitiatedSpellCast = false,
                IsProxy                = true
            });
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

            // enqueue removal of existing vanity pet if summoned
            if (player.VanityPetGuid != null)
            {
                VanityPet oldVanityPet = player.GetVisible<VanityPet>(player.VanityPetGuid.Value);
                oldVanityPet?.RemoveFromMap();
                player.VanityPetGuid = 0u;
            }

            var vanityPet = new VanityPet(player, info.Entry.DataBits00);
            player.Map.EnqueueAdd(vanityPet, player.Position);
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
                case EffectModifySpellCooldownType.Spell4:
                    player.SpellManager.SetSpellCooldown(info.Entry.DataBits01, BitConverter.Int32BitsToSingle((int)info.Entry.DataBits02));
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
                case EffectForceSpellRemoveType.Spell4:
                    Spell activeSpell4 = target.GetActiveSpell(i => i.parameters.SpellInfo.Entry.Id == info.Entry.DataBits01);
                    if (activeSpell4 != null)
                        activeSpell4.Finish();
                    break;
                case EffectForceSpellRemoveType.SpellBase:
                    Spell activeSpellBase = target.GetActiveSpell(i => i.parameters.SpellInfo.Entry.Spell4BaseIdBaseSpell == info.Entry.DataBits01);
                    if (activeSpellBase != null)
                        activeSpellBase.Finish();
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
            target.ModifyVital(vital, info.Entry.DataBits01);
        }
    }
}
