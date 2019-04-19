using NexusForever.WorldServer.Game.Entity.Static;

namespace NexusForever.WorldServer.Game.Entity
{
    public class PropertyModifier
    {
        public ModifierType ModifierType { get; private set; }
        public float Value { get; private set; }
        public uint StackCount { get; private set; }

        public PropertyModifier(ModifierType modifierType, float value)
        {
            ModifierType = modifierType;
            Value = value;
        }

        public PropertyModifier(ModifierType modifierType, float value, uint stackCount)
        {
            ModifierType = modifierType;
            Value = value;
            StackCount = stackCount;
        }
    }
}
