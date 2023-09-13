using System;
using NexusForever.WorldServer.Game.Prerequisite.Static;

namespace NexusForever.WorldServer.Game.Prerequisite
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PrerequisiteCheckAttribute : Attribute
    {
        public PrerequisiteType Type { get; }

        public PrerequisiteCheckAttribute(PrerequisiteType type)
        {
            Type = type;
        }
    }
}
