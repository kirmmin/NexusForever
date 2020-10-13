using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Prerequisite.Static
{
    [Flags]
    public enum PrerequisiteEntryFlag
    {
        EvaluateAND,
        EvaluateOR,
    }
}
