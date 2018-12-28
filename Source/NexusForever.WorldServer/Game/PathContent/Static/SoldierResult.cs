using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.PathContent.Static
{
    public enum SoldierResult
    {
        FailUnknown        = 0,
        FailTimeOut        = 1,
        FailDefenceDeath   = 2,
        FailLostResources  = 3,
        FailNoParticipants = 4,
        FailLeaveArea      = 5,
        FailDeath          = 6,
        FailParticipation  = 7,
        ScriptCancel       = 8,
        Success            = 9
    }
}
