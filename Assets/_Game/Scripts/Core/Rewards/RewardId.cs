namespace VertigoWheel.Core.Rewards
{
    /// <summary>
    /// Stable reward identities. Append new values with a unique number; never
    /// renumber existing members or reuse retired numbers in serialized data.
    /// </summary>
    public enum RewardId
    {
        None = 0,
        Cash = 1,
        Gold = 2,
        RiflePoints = 3,
        ArmorPoints = 4,
        M67Grenade = 5,
        Regenerator = 6,
        BronzeChest = 7,
        SilverChest = 8,
        GoldChest = 9,
        SuperChest = 10,
        SummerViceBayonet = 11,
        Tier3Sniper = 12,
        Tier3Shotgun = 13,
        Tier3Smg = 14
    }
}
