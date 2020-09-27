using System.Collections.Generic;
using NexusForever.Database.Configuration;
using NexusForever.Shared.Configuration;

namespace NexusForever.WorldServer
{
    public class WorldServerConfiguration
    {
        public struct MapConfig
        {
            public string MapPath { get; set; }
            public List<ushort> PrecacheBaseMaps { get; set; }
            public List<ushort> PrecacheMapSpawns { get; set; }
            public uint? GridActionThreshold { get; set; }
            public double? GridUnloadTimer { get; set; }
        }

        public struct ContactLimits
        {
            public uint? MaxFriends { get; set; }
            public uint? MaxRivals { get; set; }
            public uint? MaxIgnored { get; set; }
            public float? MaxRequestDuration { get; set; }
        }

        public NetworkConfig Network { get; set; }
        public DatabaseConfig Database { get; set; }
        public MapConfig Map { get; set; }
        public bool UseCache { get; set; } = false;
        public ushort RealmId { get; set; }
        public string MessageOfTheDay { get; set; }
        public uint LengthOfInGameDay { get; set; }
        public bool CrossFactionChat { get; set; } = true;
        public ContactLimits Contacts { get; set; }
    }
}
