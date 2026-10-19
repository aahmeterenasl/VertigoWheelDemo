using System;
using System.Collections.Generic;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Vertigo Wheel/Settings/Game")]
    public sealed class GameSettings : ScriptableObject
    {
        [SerializeField]
        private ZoneSettings _zoneSettings;

        [SerializeField]
        private WheelSettings _bronzeWheel;

        [SerializeField]
        private WheelSettings _silverWheel;

        [SerializeField]
        private WheelSettings _goldenWheel;

        public ZoneSettings ZoneSettings
        {
            get => _zoneSettings;
            set => _zoneSettings = value;
        }

        public WheelSettings BronzeWheel
        {
            get => _bronzeWheel;
            set => _bronzeWheel = value;
        }

        public WheelSettings SilverWheel
        {
            get => _silverWheel;
            set => _silverWheel = value;
        }

        public WheelSettings GoldenWheel
        {
            get => _goldenWheel;
            set => _goldenWheel = value;
        }

        public WheelSettings GetWheelSettings(ZoneType zoneType)
        {
            switch (zoneType)
            {
                case ZoneType.Normal:
                    return _bronzeWheel;
                case ZoneType.Safe:
                    return _silverWheel;
                case ZoneType.Super:
                    return _goldenWheel;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zoneType));
            }
        }

        public void Validate()
        {
            ValidateAndCreateRules();
        }

        public WheelData CreateWheelData(int zoneNumber)
        {
            ZoneRules rules = ValidateAndCreateRules();
            return GetWheelSettings(rules.GetZoneType(zoneNumber)).CreateData(zoneNumber, rules);
        }

        private ZoneRules ValidateAndCreateRules()
        {
            if (_zoneSettings == null || _bronzeWheel == null || _silverWheel == null || _goldenWheel == null)
            {
                throw new InvalidOperationException($"Game settings '{name}' needs zone settings and all three wheels.");
            }

            ZoneRules rules = _zoneSettings.CreateRules();
            _bronzeWheel.Validate(ZoneType.Normal);
            _silverWheel.Validate(ZoneType.Safe);
            _goldenWheel.Validate(ZoneType.Super);

            Dictionary<RewardId, RewardSettings> rewards = new Dictionary<RewardId, RewardSettings>();
            ValidateRewardIds(_bronzeWheel, rewards);
            ValidateRewardIds(_silverWheel, rewards);
            ValidateRewardIds(_goldenWheel, rewards);
            return rules;
        }

        private static void ValidateRewardIds(WheelSettings wheel, Dictionary<RewardId, RewardSettings> rewards)
        {
            for (int index = 0; index < wheel.SlotCount; index++)
            {
                WheelSlotSettings slot = wheel.GetSlot(index);
                if (slot.OutcomeKind != SlotOutcomeKind.Reward)
                {
                    continue;
                }

                RewardSettings reward = slot.Reward;
                if (rewards.TryGetValue(reward.RewardId, out RewardSettings existing) && existing != reward)
                {
                    throw new InvalidOperationException($"Two reward assets share the ID '{reward.RewardId}'.");
                }

                rewards[reward.RewardId] = reward;
            }
        }
    }
}
