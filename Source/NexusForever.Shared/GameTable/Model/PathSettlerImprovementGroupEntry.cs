namespace NexusForever.Shared.GameTable.Model
{
    public class PathSettlerImprovementGroupEntry
    {
        public uint Id;
        public uint PathSettlerHubId;
        public uint PathSettlerImprovementGroupFlags;
        public uint Creature2IdDepot;
        public uint LocalizedTextIdName;
        public uint SettlerAvenueTypeEnum;
        public uint ContributionValue;
        public uint PerTierBonusContributionValue;
        public uint DurationPerBundleMs;
        public uint MaxBundleCount;
        public uint PathSettlerImprovementGroupIdOutpostRequired;
        [GameTableFieldArray(4u)]
        public uint[] PathSettlerImprovementTiers;
        public uint WorldLocation2IdDisplayPoint;
    }
}
