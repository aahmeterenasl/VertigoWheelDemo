using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Vertigo Wheel/Settings/Game")]
    public sealed class GameSettings : ScriptableObject, IRunSettingsSource
    {
        [SerializeField]
        private ZoneSettings _zoneSettings;

        [SerializeField]
        private WheelSettings _bronzeWheel;

        [SerializeField]
        private WheelSettings _silverWheel;

        [SerializeField]
        private WheelSettings _goldenWheel;

        [SerializeField]
        private AnimationSettings _animationSettings;

        [SerializeField]
        private Sprite _bombIcon;

        [SerializeField]
        private ZoneWheelOverrideSettings[] _zoneOverrides = Array.Empty<ZoneWheelOverrideSettings>();

        public int ZoneOverrideCount => _zoneOverrides?.Length ?? 0;

        public ZoneWheelOverrideSettings GetZoneOverride(int index)
        {
            if (index < 0 || index >= ZoneOverrideCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _zoneOverrides[index];
        }

        public void SetZoneOverrides(ZoneWheelOverrideSettings[] overrides)
        {
            _zoneOverrides = overrides == null
                ? throw new ArgumentNullException(nameof(overrides))
                : (ZoneWheelOverrideSettings[])overrides.Clone();
        }

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

        public AnimationSettings AnimationSettings
        {
            get => _animationSettings;
            set => _animationSettings = value;
        }

        public Sprite BombIcon
        {
            get => _bombIcon;
            set => _bombIcon = value;
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

        /// <summary>Creates a detached lookup of the validated rewards referenced by active authoring sources.</summary>
        public IReadOnlyDictionary<RewardId, RewardSettings> CreateRewardCatalog()
        {
            ValidateAndCreateRules(out Dictionary<RewardId, RewardSettings> rewards);
            return new ReadOnlyDictionary<RewardId, RewardSettings>(rewards);
        }

        public ZoneRules CreateZoneRules()
        {
            if (_zoneSettings == null)
            {
                throw new InvalidOperationException($"Game settings '{name}' needs zone settings.");
            }

            return _zoneSettings.CreateRules();
        }

        public WheelSettings GetWheelSettingsForZone(int zoneNumber)
        {
            ZoneRules rules = ValidateAndCreateRules();
            return FindOverride(zoneNumber)?.Wheel ?? GetWheelSettings(rules.GetZoneType(zoneNumber));
        }

        public WheelData CreateWheelData(int zoneNumber, IRandomSource random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
            ZoneRules rules = ValidateAndCreateRules();
            ZoneWheelOverrideSettings exact = FindOverride(zoneNumber);
            return exact != null
                ? exact.Wheel.CreateData(zoneNumber, rules)
                : GetWheelSettings(rules.GetZoneType(zoneNumber)).CreateFromPool(zoneNumber, rules, random);
        }

        private ZoneRules ValidateAndCreateRules()
        {
            return ValidateAndCreateRules(out _);
        }

        private ZoneRules ValidateAndCreateRules(out Dictionary<RewardId, RewardSettings> rewards)
        {
            if (_zoneSettings == null || _bronzeWheel == null || _silverWheel == null || _goldenWheel == null)
            {
                throw new InvalidOperationException($"Game settings '{name}' needs zone settings and all three wheels.");
            }

            ZoneRules rules = _zoneSettings.CreateRules();
            _bronzeWheel.ValidatePool();
            _silverWheel.ValidatePool();
            _goldenWheel.ValidatePool();

            rewards = new Dictionary<RewardId, RewardSettings>();
            ValidatePoolRewardIds(_bronzeWheel.Pool, rewards);
            ValidatePoolRewardIds(_silverWheel.Pool, rewards);
            ValidatePoolRewardIds(_goldenWheel.Pool, rewards);
            HashSet<int> zones = new HashSet<int>();
            for (int index = 0; index < ZoneOverrideCount; index++)
            {
                ZoneWheelOverrideSettings exact = _zoneOverrides[index];
                if (exact == null)
                {
                    throw new InvalidOperationException("A zone override is missing.");
                }
                exact.Validate();
                if (!zones.Add(exact.ZoneNumber))
                {
                    throw new InvalidOperationException($"Zone {exact.ZoneNumber} has multiple wheel overrides.");
                }

                exact.Wheel.CreateData(exact.ZoneNumber, rules);
                ValidateRewardIds(exact.Wheel, rewards);
            }
            return rules;
        }

        private ZoneWheelOverrideSettings FindOverride(int zoneNumber)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber));
            }
            for (int index = 0; index < ZoneOverrideCount; index++)
            {
                if (_zoneOverrides[index].ZoneNumber == zoneNumber)
                {
                    return _zoneOverrides[index];
                }
            }

            return null;
        }

        private static void ValidatePoolRewardIds(RewardPoolSettings pool, Dictionary<RewardId, RewardSettings> rewards)
        {
            for (int index = 0; index < pool.EntryCount; index++)
            {
                RegisterReward(pool.GetEntry(index).RewardSlot.Reward, rewards);
            }
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

                RegisterReward(slot.Reward, rewards);
            }
        }

        private static void RegisterReward(RewardSettings reward, Dictionary<RewardId, RewardSettings> rewards)
        {
            if (rewards.TryGetValue(reward.RewardId, out RewardSettings existing) && existing != reward)
            {
                throw new InvalidOperationException($"Two reward assets share the ID '{reward.RewardId}'.");
            }

            rewards[reward.RewardId] = reward;
        }
    }
}
