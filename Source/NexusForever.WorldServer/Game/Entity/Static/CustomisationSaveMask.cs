using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Entity.Static
{
    [Flags]
    public enum CustomisationSaveMask
    {
        None    = 0x0000,
        Create  = 0x0001,
        Modify  = 0x0002,
        Delete  = 0x0004
    }
}
