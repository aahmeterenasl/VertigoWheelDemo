using System;
using UnityEngine;
using VertigoWheel.Core.Rewards;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "RewardSettings", menuName = "Vertigo Wheel/Settings/Reward")]
    public sealed class RewardSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Reward identity. Select a defined reward other than None; keep its enum number stable.")]
        private RewardId _rewardId = RewardId.None;

        [SerializeField]
        private string _displayName = string.Empty;

        [SerializeField]
        private Sprite _icon;

        public RewardId RewardId
        {
            get => _rewardId;
            set
            {
                if (!IsValidId(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Select a defined reward other than None.");
                }

                _rewardId = value;
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("A reward needs a display name.", nameof(value));
                }

                _displayName = value;
            }
        }

        public Sprite Icon
        {
            get => _icon;
            set => _icon = value;
        }

        public void Validate()
        {
            if (!IsValidId(_rewardId) || string.IsNullOrWhiteSpace(_displayName) || _icon == null)
            {
                throw new InvalidOperationException(
                    $"Reward '{name}' requires a defined ID other than None, a display name, and an icon.");
            }
        }

        private static bool IsValidId(RewardId value)
        {
            return value != RewardId.None && Enum.IsDefined(typeof(RewardId), value);
        }
    }
}
