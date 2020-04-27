using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Entity.Static
{
    [Flags]
    public enum CharacterFlag
    {
        None                  = 0x0000,
        FriendRequestsBlocked = 0x0002,
        IgnoreDuelRequests    = 0x0008,
        HolomarkVisibleLeft   = 0x0040,
        HolomarkVisibleRight  = 0x0080,
        HolomarkVisibleBack   = 0x0100,
        HolomarkNear          = 0x0200,
    }
}
