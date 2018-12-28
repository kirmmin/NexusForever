using System;
using System.Collections.Generic;

namespace NexusForever.Database.Character.Model
{
    public partial class CharacterPathMissionModel
    {
        public ulong Id { get; set; }
        public uint EpisodeId { get; set; }
        public uint MissionId { get; set; }
        public uint Progress { get; set; }
        public byte Complete { get; set; }
        public byte Unlocked { get; set; }

        public CharacterPathEpisodeModel CharacterPathEpisode { get; set; }
    }
}
