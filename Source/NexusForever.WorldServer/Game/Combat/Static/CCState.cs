using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Combat.Static
{
    public enum CCState
    {
        Stun                = 0x0000,
        Sleep               = 0x0001,
        Root                = 0x0002,
        Disarm              = 0x0003,
        Silence             = 0x0004,
        Polymorph           = 0x0005,
        Fear                = 0x0006,
        Hold                = 0x0007,
        Knockdown           = 0x0008,
        Vulnerability       = 0x0009,
        VulnerabilityWithAct = 0x000A,
        Disorient           = 0x000B,
        Disable             = 0x000C,
        Taunt               = 0x000D,
        DeTaunt             = 0x000E,
        Blind               = 0x000F,
        Knockback           = 0x0010,
        Pushback            = 0x0011,
        Pull                = 0x0012,
        PositionSwitch      = 0x0013,
        Tether              = 0x0014,
        Snare               = 0x0015,
        Interrupt           = 0x0016,
        Daze                = 0x0017,
        Subdue              = 0x0018,
        Grounded            = 0x0019,
        DisableCinematic    = 0x001A,
        AbilityRestriction  = 0x001B
    }
}
