using System;
using UnityEngine;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "ZoneSettings", menuName = "Vertigo Wheel/Settings/Zones")]
    public sealed class ZoneSettings : ScriptableObject
    {
        [SerializeField, Min(1)]
        private int _safeZoneInterval = 5;

        [SerializeField, Min(1)]
        private int _superZoneInterval = 30;

        [SerializeField]
        private CashOutPolicy _cashOutPolicy = CashOutPolicy.AnyZone;

        [SerializeField, Min(1)]
        [Tooltip("Number of zones per reward tier. Zone 1 starts at tier 0.")]
        private int _rewardTierInterval = 5;

        [SerializeField, Min(1)]
        [Tooltip("Upper bound for a single slot's reward quantity, not the run total.")]
        private long _maximumRewardQuantity = 1000000000;

        public int SafeZoneInterval
        {
            get => _safeZoneInterval;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _safeZoneInterval = value;
            }
        }

        public int SuperZoneInterval
        {
            get => _superZoneInterval;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _superZoneInterval = value;
            }
        }

        public CashOutPolicy CashOutPolicy
        {
            get => _cashOutPolicy;
            set
            {
                if (!Enum.IsDefined(typeof(CashOutPolicy), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _cashOutPolicy = value;
            }
        }

        public int RewardTierInterval
        {
            get => _rewardTierInterval;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _rewardTierInterval = value;
            }
        }

        public long MaximumRewardQuantity
        {
            get => _maximumRewardQuantity;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _maximumRewardQuantity = value;
            }
        }

        /// <summary>
        /// Read fresh rules when preparing gameplay. Changes to this asset do not
        /// mutate an existing rules snapshot or an already accepted spin.
        /// Also validates serialized values, which bypass property setters.
        /// </summary>
        public ZoneRules CreateRules()
        {
            return new ZoneRules(
                _safeZoneInterval,
                _superZoneInterval,
                _cashOutPolicy,
                _rewardTierInterval,
                _maximumRewardQuantity);
        }
    }
}
