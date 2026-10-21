using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Rewards
{
    /// <summary>One reusable reward row or summary tile; layout belongs to its prefab.</summary>
    public sealed class RewardItemView : MonoBehaviour
    {
        [SerializeField]
        private UnityEngine.UI.Image _icon;

        [SerializeField, Tooltip("Optional label used by reward tiles; compact rows omit it.")]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _quantity;

        [SerializeField]
        private bool _showQuantityPrefix = true;

        private RewardId _rewardId;

        public RewardId RewardId => _rewardId;
        public RectTransform IconTransform => _icon != null ? _icon.rectTransform : null;

        public void Bind(RewardSettings reward, long quantity)
        {
            if (reward == null)
            {
                throw new ArgumentNullException(nameof(reward));
            }
            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }
            reward.Validate();
            ValidateReferences();

            _rewardId = reward.RewardId;
            _icon.sprite = reward.Icon;
            _icon.preserveAspect = true;
            if (_name != null)
            {
                _name.text = reward.DisplayName;
            }
            _quantity.text = (_showQuantityPrefix ? "x" : string.Empty)
                + quantity.ToString("N0", CultureInfo.InvariantCulture);
        }

        internal void ValidateReferences()
        {
            if (_icon == null || _quantity == null)
            {
                throw new InvalidOperationException("A reward item is missing its icon or quantity reference.");
            }
        }
    }
}
