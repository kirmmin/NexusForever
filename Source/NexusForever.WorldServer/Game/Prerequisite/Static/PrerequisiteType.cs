namespace NexusForever.WorldServer.Game.Prerequisite.Static
{
    // TODO: name these from PrerequisiteType.tbl error messages
    public enum PrerequisiteType
    {
        None                = 0,
        Level               = 1,
        Race                = 2,
        Class               = 3,
        Faction             = 4,
        Reputation          = 5,
        Quest               = 6,
        Achievement         = 7,
        Prerequisite        = 11,
        /// <summary>
        /// Checks for whether or not the Player is affected by this spell. Used in cases to check for if player has AMP.
        /// </summary>
        Spell               = 15,
        /// <summary>
        /// Appears to check whether the user is in combat. Error Msg: "You must be in combat"
        /// </summary>
        InCombat            = 28,
        TargetIsPlayer      = 39,
        QuestObjective      = 43,
        /// <summary>
        /// Checks for whether or not the Player has an AMP.
        /// </summary>
        HasBuff             = 50,
        Path                = 52,
        /// <summary>
        /// Checks the amount of a Spell4Id is applied.
        /// </summary>
        ActiveSpellCount    = 59,
        /// <summary>
        /// Checks the amount of a given SpellMechanic resource. Seems to be legacy version of Vitals.
        /// </summary>
        SpellMechanic       = 64,
        QuestObjective2     = 68,
        VitalPercent        = 71,
        Vital               = 73,
        /// <summary>
        /// Checks to see if a PositionalRequirement Entry is met.
        /// </summary>
        PositionalRequirement = 108,
        /// <summary>
        /// Checks for an objectId. Used in the "RavelSignal" SpellEffectType at minimum. Error: World requirement not met
        /// </summary>
        WorldReq            = 109,
        Stealth             = 116,
        SpellObj            = 129,
        /// <summary>
        /// Checks for an ObjectId, which is a hashed petflair id.
        /// </summary>
        HoverboardFlair     = 190,
        /// <summary>
        /// Used for Mount checks
        /// </summary>
        Unknown194          = 194,
        /// <summary>
        /// Used for Mount checks
        /// </summary>
        Unknown195          = 195,
        SpellBaseId         = 214,
        Amp                 = 227,
        Plane               = 232,
        Faction2            = 243,
        AccountItemClaimed  = 246,
        BaseFaction         = 250,
        Unhealthy           = 269,
        Entitlement         = 273,
        CostumeUnlocked     = 275,
        CosmicRewards       = 270,
        PurchasedTitle      = 288
    }
}
