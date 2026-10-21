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

        public int SafeZoneInterval => _safeZoneInterval;
        public int SuperZoneInterval => _superZoneInterval;

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
            return AllowsCashOut(GetZoneType(zoneNumber));
        }

        /// <summary>Evaluate current policy against an already prepared wheel's type.</summary>
        public bool AllowsCashOut(ZoneType zoneType)
        {
            if (!Enum.IsDefined(typeof(ZoneType), zoneType))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneType));
            }

            return _cashOutPolicy == CashOutPolicy.AnyZone || zoneType != ZoneType.Normal;
        }

        public int GetRewardTier(int zoneNumber)
        {
            ValidateZoneNumber(zoneNumber);
            return (zoneNumber - 1) / _rewardTierInterval;
        }

        /// <summary>Per-zone linear growth, then wheel scaling; both saturate safely at the payout cap.</summary>
        public long GetRewardQuantity(int zoneNumber, long baseQuantity, long increasePerZone, int quantityMultiplier = 1)
        {
            ValidateZoneNumber(zoneNumber);
            if (baseQuantity < 1) throw new ArgumentOutOfRangeException(nameof(baseQuantity));
            if (increasePerZone < 0) throw new ArgumentOutOfRangeException(nameof(increasePerZone));
            if (quantityMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(quantityMultiplier));

            long elapsedZones = (long)zoneNumber - 1;
            long quantity = Math.Min(baseQuantity, _maximumRewardQuantity);
            long remaining = _maximumRewardQuantity - quantity;
            if (elapsedZones > 0 && increasePerZone > remaining / elapsedZones)
                return _maximumRewardQuantity;
            quantity += increasePerZone * elapsedZones;

            // Scaling a saturated positive quantity cannot bring it back below the cap.
            // Divide before multiplying, so even long.MaxValue and int.MaxValue are safe.
            return quantity > _maximumRewardQuantity / quantityMultiplier
                ? _maximumRewardQuantity : quantity * quantityMultiplier;
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
