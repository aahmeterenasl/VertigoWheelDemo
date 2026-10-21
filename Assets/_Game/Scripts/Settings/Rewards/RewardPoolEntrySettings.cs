using System;
using UnityEngine;
using VertigoWheel.Core.Wheels;

namespace VertigoWheel.Settings
{
    [Serializable]
    public sealed class RewardPoolEntrySettings
    {
        [SerializeField]
        private WheelSlotSettings _rewardSlot = new WheelSlotSettings();

        [SerializeField, Min(0)]
        [Tooltip("Relative chance of appearing on a generated wheel. Zero disables this entry.")]
        private int _poolWeight = 1;

        [SerializeField, Min(0)]
        [Tooltip("First eligible pool tier, using ZoneSettings.RewardTierInterval. Independent of quantity growth.")]
        private int _minimumTier;

        [SerializeField, Min(-1)]
        [Tooltip("Inclusive upper tier. -1 means no upper limit.")]
        private int _maximumTier = -1;

        public WheelSlotSettings RewardSlot
        {
            get => _rewardSlot;
            set => _rewardSlot = value ?? throw new ArgumentNullException(nameof(value));
        }

        public int PoolWeight
        {
            get => _poolWeight;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _poolWeight = value;
            }
        }

        public int MinimumTier
        {
            get => _minimumTier;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _minimumTier = value;
            }
        }

        public int MaximumTier
        {
            get => _maximumTier;
            set
            {
                if (value < -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _maximumTier = value;
            }
        }

        public bool IsEligible(int tier)
        {
            if (tier < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tier));
            }
            return _poolWeight > 0 && tier >= _minimumTier && (_maximumTier == -1 || tier <= _maximumTier);
        }

        public void Validate()
        {
            if (_rewardSlot == null || _poolWeight < 0 || _minimumTier < 0
                || _maximumTier < -1 || (_maximumTier != -1 && _maximumTier < _minimumTier))
            {
                throw new InvalidOperationException("Pool entry has an invalid reward, weight, or tier range.");
            }

            _rewardSlot.Validate();
            if (_rewardSlot.OutcomeKind != SlotOutcomeKind.Reward)
            {
                throw new InvalidOperationException("Bombs cannot be authored in reward pools.");
            }
        }
    }
}
