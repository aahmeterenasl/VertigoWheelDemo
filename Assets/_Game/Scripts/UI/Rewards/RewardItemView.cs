using System;
using System.Globalization;
using DG.Tweening;
using VertigoWheel.UI.Common;
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

        [SerializeField] private UiMotionView _motion;
        private RewardId _rewardId;
        private long _quantityTarget;
        private Tween _counter;
        private bool _holding;
        private bool _newArrival;

        public UiMotionView Motion => _motion;
        public long Quantity => _quantityTarget;

        public void HoldQuantity(long previous)
        {
            _holding = true;
            _newArrival = previous == 0L;
            SetQuantity(previous);
        }

        public void Arrive(long increment, AnimationSettings settings, bool visible)
        {
            _holding = false;
            _counter?.Kill(false);
            _counter = null;
            if (!visible || settings.LedgerDurationSeconds <= 0f)
            {
                SetQuantity(_quantityTarget);
                return;
            }
            long previous = Math.Max(0L, _quantityTarget - increment);
            float progress = 0f;
            _counter = DOTween.To(() => progress, value =>
            {
                progress = value;
                // Decimal arithmetic preserves integer totals, including long.MaxValue.
                SetQuantity(previous + (long)decimal.Floor((decimal)(_quantityTarget - previous) * (decimal)Mathf.Clamp01(value)));
            }, 1f, settings.LedgerDurationSeconds).SetEase(Ease.OutCubic).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(FinishFeedback);
            if (_newArrival) _motion?.Reveal(settings.LedgerDurationSeconds, 0f, 6f, 1f);
            else _motion?.Pulse(settings.LedgerDurationSeconds, settings.LedgerPulseScale);
        }

        public void FinishFeedback()
        {
            _counter?.Kill(false);
            _counter = null;
            _holding = false;
            SetQuantity(_quantityTarget);
            _motion?.Complete();
        }

        private void SetQuantity(long quantity)
        {
            if (_quantity != null) _quantity.text = (_showQuantityPrefix ? "x" : string.Empty)
                + quantity.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void OnDisable() => FinishFeedback();

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

            bool sameBinding = _rewardId == reward.RewardId && _quantityTarget == quantity;
            if (!sameBinding) FinishFeedback();
            _rewardId = reward.RewardId;
            _quantityTarget = quantity;
            _icon.sprite = reward.Icon;
            _icon.preserveAspect = true;
            if (_name != null)
            {
                _name.text = reward.DisplayName;
            }
            if (!_holding && _counter == null) SetQuantity(quantity);
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
