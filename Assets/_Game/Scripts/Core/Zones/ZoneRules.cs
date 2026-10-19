using System;

namespace VertigoWheel.Core.Zones
{
    /// <summary>
    /// A snapshot of zone rules. RunSession will also enforce state eligibility
    /// before allowing cash out; this type only checks the zone policy.
    /// </summary>
    public sealed class ZoneRules
    {
        private readonly int _safeZoneInterval;
        private readonly int _superZoneInterval;
        private readonly CashOutPolicy _cashOutPolicy;
        private readonly int _rewardTierInterval;
        private readonly long _maximumRewardQuantity;

        public ZoneRules(
            int safeZoneInterval,
            int superZoneInterval,
            CashOutPolicy cashOutPolicy,
            int rewardTierInterval,
            long maximumRewardQuantity)
        {
            if (safeZoneInterval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(safeZoneInterval));
            }

            if (superZoneInterval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(superZoneInterval));
            }

            if (!Enum.IsDefined(typeof(CashOutPolicy), cashOutPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(cashOutPolicy));
            }

            if (rewardTierInterval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rewardTierInterval));
            }

            if (maximumRewardQuantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRewardQuantity));
            }

            _safeZoneInterval = safeZoneInterval;
            _superZoneInterval = superZoneInterval;
            _cashOutPolicy = cashOutPolicy;
            _rewardTierInterval = rewardTierInterval;
            _maximumRewardQuantity = maximumRewardQuantity;
        }

        public ZoneType GetZoneType(int zoneNumber)
        {
            ValidateZoneNumber(zoneNumber);

            // Super zones take priority where both intervals match.
            if (zoneNumber % _superZoneInterval == 0)
            {
                return ZoneType.Super;
            }

            return zoneNumber % _safeZoneInterval == 0
                ? ZoneType.Safe
                : ZoneType.Normal;
        }

        public bool AllowsCashOut(int zoneNumber)
        {
            ValidateZoneNumber(zoneNumber);

            return _cashOutPolicy == CashOutPolicy.AnyZone
                || GetZoneType(zoneNumber) != ZoneType.Normal;
        }

        public int GetRewardTier(int zoneNumber)
        {
            ValidateZoneNumber(zoneNumber);
            return (zoneNumber - 1) / _rewardTierInterval;
        }

        public long GetRewardQuantity(int zoneNumber, long baseQuantity, long increasePerTier)
        {
            if (baseQuantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(baseQuantity));
            }

            if (increasePerTier < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(increasePerTier));
            }

            int tier = GetRewardTier(zoneNumber);
            long quantity = Math.Min(baseQuantity, _maximumRewardQuantity);

            if (tier == 0 || increasePerTier == 0)
            {
                return quantity;
            }

            // Check remaining capacity before multiplying to avoid long overflow.
            long remaining = _maximumRewardQuantity - quantity;
            if (increasePerTier > remaining / tier)
            {
                return _maximumRewardQuantity;
            }

            return quantity + increasePerTier * tier;
        }

        private static void ValidateZoneNumber(int zoneNumber)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber));
            }
        }
    }
}
