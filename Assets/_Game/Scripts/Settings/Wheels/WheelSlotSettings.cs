using System;
using UnityEngine;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Settings
{
    [Serializable]
    public sealed class WheelSlotSettings
    {
        [SerializeField]
        private SlotOutcomeKind _outcomeKind;

        [SerializeField]
        private RewardSettings _reward;

        [SerializeField, Min(0)]
        private long _baseQuantity = 1;

        [SerializeField, Min(0)]
        private long _quantityIncreasePerTier = 1;

        [SerializeField, Min(1)]
        [Tooltip("Relative chance of landing on this displayed slot. Equal values give equal chances.")]
        private int _landingWeight = 1;

        public int LandingWeight
        {
            get => _landingWeight;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _landingWeight = value;
            }
        }

        public SlotOutcomeKind OutcomeKind
        {
            get => _outcomeKind;
            set
            {
                if (!Enum.IsDefined(typeof(SlotOutcomeKind), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _outcomeKind = value;
            }
        }

        public RewardSettings Reward
        {
            get => _reward;
            set => _reward = value;
        }

        public long BaseQuantity
        {
            get => _baseQuantity;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _baseQuantity = value;
            }
        }

        public long QuantityIncreasePerTier
        {
            get => _quantityIncreasePerTier;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _quantityIncreasePerTier = value;
            }
        }

        public void Validate()
        {
            if (_landingWeight < 1)
            {
                throw new InvalidOperationException("Every displayed slot must have a positive landing weight.");
            }

            if (!Enum.IsDefined(typeof(SlotOutcomeKind), _outcomeKind))
            {
                throw new InvalidOperationException("A wheel slot has an unknown outcome kind.");
            }

            if (_outcomeKind == SlotOutcomeKind.Bomb)
            {
                if (_reward != null || _baseQuantity != 0 || _quantityIncreasePerTier != 0)
                {
                    throw new InvalidOperationException("A bomb slot must have no reward and zero quantities.");
                }

                return;
            }

            if (_reward == null || _baseQuantity < 1 || _quantityIncreasePerTier < 0)
            {
                throw new InvalidOperationException("A reward slot needs a reward, positive quantity, and non-negative growth.");
            }

            _reward.Validate();
        }

        internal WheelSlotData CreateData(int zoneNumber, ZoneRules rules)
        {
            Validate();
            return _outcomeKind == SlotOutcomeKind.Bomb
                ? WheelSlotData.CreateBomb(_landingWeight)
                : WheelSlotData.CreateReward(
                    _reward.RewardId,
                    rules.GetRewardQuantity(zoneNumber, _baseQuantity, _quantityIncreasePerTier),
                    _landingWeight);
        }
    }
}
