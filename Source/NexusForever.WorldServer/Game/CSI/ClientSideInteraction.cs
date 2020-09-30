using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.CSI.Static;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Spell;
using NLog;
using System;
using System.Linq;

namespace NexusForever.WorldServer.Game.CSI
{
    public class ClientSideInteraction
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public uint ClientUniqueId { get; private set; } = 0;
        public WorldEntity ActivateUnit { get; private set; } = null;
        public CSIType CsiType { get; private set; } = CSIType.Interaction;
        public ClientSideInteractionEntry Entry { get; private set; }

        private Player Owner { get; set; } = null;

        public ClientSideInteraction(Player owner, WorldEntity activateUnit, uint clientUniqueId)
        {
            Owner = owner;
            ActivateUnit = activateUnit;
            ClientUniqueId = clientUniqueId;
        }

        public void TriggerReady()
        {
            // This should be used for things like responding after a timer, or ensuring an event happens during an interaction's cast time.
        }

        public void TriggerFail()
        {
            ActivateUnit.OnActivateFail(Owner);
        }

        public void TriggerSuccess()
        {
            ActivateUnit.OnActivateSuccess(Owner);
        }

        public void SetClientSideInteractionEntry(ClientSideInteractionEntry clientSideInteractionEntry)
        {
            if (clientSideInteractionEntry == null)
            {
                CsiType = CSIType.Interaction;
                return;
            }

            Entry = clientSideInteractionEntry;
            CsiType = (CSIType)Entry.InteractionType;
        }

        public void HandleSuccess(SpellParameters spellCast)
        {
            Creature2Entry entry = GameTableManager.Instance.Creature2.GetEntry(ActivateUnit.CreatureId);
            if (entry == null)
                throw new ArgumentNullException($"Creature2Entry was null in CSI for CreatureId {ActivateUnit.CreatureId}");

            // TODO: Handle casting activate spells at correct times. Additionally, ensure Prerequisites are met to cast.
            // Creature2Entry can contain up to 4 spells to activate and prerequisite spells to trigger.
            uint spell4Id = spellCast.SpellInfo.Entry.Id;
            if (entry.Spell4IdActivate.Length > 0)
            {
                uint[] spell4Ids = entry.Spell4IdActivate.Where(s => s != 0u).ToArray();

                for (int currentIndex = spell4Ids.Length - 1; currentIndex > -1; currentIndex--)
                {
                    if (spell4Ids[currentIndex] == spell4Id && currentIndex == 0)
                    {
                        TriggerSuccess();
                        break;
                    }

                    if (spell4Ids[currentIndex] == spell4Id && currentIndex > 0)
                    {
                        SpellParameters parameters = new SpellParameters
                        {
                            PrimaryTargetId = ActivateUnit.Guid,
                            CompleteAction = HandleSuccess
                        };
                        Owner.CastSpell(entry.Spell4IdActivate[currentIndex - 1], parameters);
                    }
                }
            }
        }
    }
}
