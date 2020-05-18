using System;

namespace NexusForever.WorldServer.Script
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ScriptAttribute : Attribute
    {
        public uint Id { get; }

        public ScriptAttribute(uint creatureId)
        {
            Id = creatureId;
        }
    }
}
