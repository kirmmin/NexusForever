using System;
using System.Linq;
using System.Numerics;
using NexusForever.Shared;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Combat;
using NexusForever.Shared.Network;
using NexusForever.WorldServer.Game.Combat.Static;
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
            // TODO: calculate damage
            info.AddDamage((DamageType)info.Entry.DamageType, 1337);
        }

        [SpellEffectHandler(SpellEffectType.Proxy)]
        private void HandleEffectProxy(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            target.CastSpell(info.Entry.DataBits00, new SpellParameters
            {
                ParentSpellInfo        = parameters.SpellInfo,
                RootSpellInfo          = parameters.RootSpellInfo,
                UserInitiatedSpellCast = false
            });
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

            if (player.VehicleGuid != 0u)
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

            // FIXME: also cast 52539,Riding License - Riding Skill 1 - SWC - Tier 1,34464
            // FIXME: also cast 80530,Mount Sprint  - Tier 2,36122
        }

        [SpellEffectHandler(SpellEffectType.Teleport)]
        private void HandleEffectTeleport(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            WorldLocation2Entry locationEntry = GameTableManager.Instance.WorldLocation2.GetEntry(info.Entry.DataBits00);
            if (locationEntry == null)
                return;

            if (target is Player player)
                player.TeleportTo((ushort)locationEntry.WorldId, locationEntry.Position0, locationEntry.Position1, locationEntry.Position2);
        }

        [SpellEffectHandler(SpellEffectType.FullScreenEffect)]
        private void HandleFullScreenEffect(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
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

        [SpellEffectHandler(SpellEffectType.CCStateSet)]
        private void HandleEffectCCStateSet(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            if (parameters.SpellInfo.Entry.Id != 1384 && info.Entry.DataBits00 == 8)
                target.CastSpell(1384, new SpellParameters
                {
                    ParentSpellInfo = parameters.SpellInfo,
                    RootSpellInfo = parameters.RootSpellInfo,
                    PrimaryTargetId = target.Guid,
                    CCDurationOverride = info.Entry.DurationTime,
                    UserInitiatedSpellCast = false
                });

            target.EnqueueToVisible(new ServerEntityCCStateSet
            {
                UnitId = target.Guid,
                State = (CCState)info.Entry.DataBits00,
                ServerUniqueEffectId = info.EffectId
            }, true);
            target.EnqueueToVisible(new Server07F5
            {
                CasterId = target.Guid,
                CastingId = CastingId,
                Unknown8 = info.EffectId
            }, true);
            target.EnqueueToVisible(new ServerCombatLog
            {
                LogType = 1,
                CCStateData = new ServerCombatLog.CCStateLog
                {
                    CCState = (CCState)info.Entry.DataBits00,
                    BRemoved = false,
                    InterruptArmorTaken = 1, // TODO: Calculate interrupt armor
                    Result = 0,
                    Unknown0 = 0,
                    CasterId = caster.Guid,
                    TargetId = target.Guid,
                    Spell4Id = parameters.SpellInfo.Entry.Id,
                    CombatResult = 8
                }
            }, true);

            double ccDuration = info.Entry.DurationTime;
            if (parameters.CCDurationOverride > 0)
                ccDuration = parameters.CCDurationOverride;

            events.EnqueueEvent(new SpellEvent(ccDuration / 1000d, () => 
            {
                target.EnqueueToVisible(new ServerEntityCCStateRemove
                {
                    UnitId = target.Guid,
                    State = (CCState)info.Entry.DataBits00,
                    CastingId = CastingId,
                    ServerUniqueEffectId = info.EffectId
                }, true);
                target.EnqueueToVisible(new ServerCombatLog
                {
                    LogType = 1,
                    CCStateData = new ServerCombatLog.CCStateLog
                    {
                        CCState = (CCState)info.Entry.DataBits00,
                        BRemoved = true,
                        InterruptArmorTaken = 1, // TODO: Calculate interrupt armor
                        Result = 10,
                        Unknown0 = 0,
                        CasterId = caster.Guid,
                        TargetId = target.Guid,
                        Spell4Id = parameters.SpellInfo.Entry.Id,
                        CombatResult = 8
                    }
                }, true);
            }));
        }

        [SpellEffectHandler(SpellEffectType.Fluff)]
        private void HandleEffectFluff(UnitEntity target, SpellTargetInfo.SpellTargetEffectInfo info)
        {
            events.EnqueueEvent(new SpellEvent(info.Entry.DurationTime / 1000d, () =>
            {

            }));
        }
    }
}
