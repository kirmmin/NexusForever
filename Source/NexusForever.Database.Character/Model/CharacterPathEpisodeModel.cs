using System;
using System.Collections.Generic;

namespace NexusForever.Database.Character.Model
{
    public partial class CharacterPathEpisodeModel
    {
        public ulong Id { get; set; }
        public uint EpisodeId { get; set; }
        public byte RewardReceived { get; set; }

        public CharacterModel Character { get; set; }
        public ICollection<CharacterPathMissionModel> PathMission { get; set; }
    }
}
