using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.WorldServer.Game.Entity.Static;

namespace NexusForever.WorldServer.Game.Entity
{
    public class Appearance
    {
        public ulong Owner { get; }
        public ItemSlot ItemSlot { get; }

        public ushort DisplayId
        {
            get => displayId;
            set
            {
                if (displayId != value)
                {
                    displayId = value;
                    saveMask |= CustomisationSaveMask.Modify;
                }
            }
        }
        private ushort displayId;

        public bool PendingDelete => (saveMask & CustomisationSaveMask.Delete) != 0;

        private CustomisationSaveMask saveMask;

        public Appearance(CharacterAppearanceModel model)
        {
            Owner = model.Id;
            ItemSlot = (ItemSlot)model.Slot;
            displayId = model.DisplayId;

            saveMask = CustomisationSaveMask.None;
        }

        public Appearance(ulong characterId, ItemSlot itemSlot, ushort displayId)
        {
            Owner = characterId;
            ItemSlot = itemSlot;
            this.displayId = displayId;

            saveMask = CustomisationSaveMask.Create;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask != CustomisationSaveMask.None)
            {
                var model = new CharacterAppearanceModel
                {
                    Id = Owner,
                    Slot = (byte)ItemSlot
                };

                EntityEntry<CharacterAppearanceModel> entity = context.Attach(model);

                if ((saveMask & CustomisationSaveMask.Create) != 0)
                {
                    model.DisplayId = DisplayId;

                    context.Add(model);
                }
                else if ((saveMask & CustomisationSaveMask.Delete) != 0)
                {
                    context.Entry(model).State = EntityState.Deleted;
                }
                else
                {
                    if ((saveMask & CustomisationSaveMask.Modify) != 0)
                    {
                        model.DisplayId = DisplayId;
                        entity.Property(e => e.DisplayId).IsModified = true;
                    }
                }
            }

            saveMask = CustomisationSaveMask.None;
        }

        public void Delete()
        {
            if ((saveMask & CustomisationSaveMask.Create) != 0)
                saveMask = CustomisationSaveMask.None;
            else
                saveMask = CustomisationSaveMask.Delete;
        }
    }
}
