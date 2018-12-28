using System;

namespace NexusForever.WorldServer.Game.PathContent.Static
{
    /// <summary>
    /// Determines which fields need saving for <see cref="PathEpisode"/> when being saved to the database.
    /// </summary>
    [Flags]
    public enum PathEpisodeSaveMask
    {
        None                = 0x0000,
        Create              = 0x0001,
        Delete              = 0x0002,
        RewardChange        = 0x0004,
    }
}
