using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Rewards
{
    /// <summary>Reuses up to the largest displayed collection instead of rebuilding on each refresh.</summary>
    public sealed class RewardCollectionView : MonoBehaviour
    {
        [SerializeField]
        private UnityEngine.UI.ScrollRect _scrollRect;

        [SerializeField]
        private RectTransform _content;

        [SerializeField]
        private RewardItemView _itemPrefab;

        [SerializeField]
        private TMP_Text _emptyLabel;

        private readonly List<RewardItemView> _items = new List<RewardItemView>();

        public void SetRewards(
            IReadOnlyList<RewardAmount> rewards,
            IReadOnlyDictionary<RewardId, RewardSettings> catalog,
            bool resetScroll = false)
        {
            if (rewards == null)
            {
                throw new ArgumentNullException(nameof(rewards));
            }
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            ValidateReferences();

            // A missing or invalid authored reward must not leave a partially refreshed ledger.
            for (int index = 0; index < rewards.Count; index++)
            {
                RewardAmount amount = rewards[index];
                if (amount == null || !catalog.TryGetValue(amount.RewardId, out RewardSettings reward)
                    || reward == null || reward.RewardId != amount.RewardId)
                {
                    throw new InvalidOperationException("The reward collection has no matching visual reward.");
                }
                reward.Validate();
            }

            Vector2 scrollPosition = _scrollRect.normalizedPosition;
            for (int index = 0; index < rewards.Count; index++)
            {
                if (index == _items.Count)
                {
                    _items.Add(Instantiate(_itemPrefab, _content, false));
                }
                RewardAmount amount = rewards[index];
                RewardItemView item = _items[index];
                item.Bind(catalog[amount.RewardId], amount.Quantity);
                item.gameObject.SetActive(true);
            }
            for (int index = rewards.Count; index < _items.Count; index++)
            {
                _items[index].gameObject.SetActive(false);
            }
            _emptyLabel.gameObject.SetActive(rewards.Count == 0);

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (resetScroll)
            {
                StopScrolling();
                _scrollRect.normalizedPosition = new Vector2(0f, 1f);
            }
            else
            {
                _scrollRect.normalizedPosition = scrollPosition;
            }
        }

        public void StopScrolling()
        {
            if (_scrollRect != null)
            {
                _scrollRect.StopMovement();
            }
        }

        public bool TryGetArrivalTarget(RewardId rewardId, out RectTransform icon, out RectTransform viewport)
        {
            icon = null;
            viewport = _scrollRect != null ? _scrollRect.viewport : null;
            if (viewport == null || !viewport.gameObject.activeInHierarchy)
            {
                return false;
            }
            foreach (RewardItemView item in _items)
            {
                if (item != null && item.gameObject.activeInHierarchy && item.RewardId == rewardId)
                {
                    icon = item.IconTransform;
                    return icon != null;
                }
            }
            return false;
        }

        public void SetInteractionEnabled(bool interactionEnabled)
        {
            if (_scrollRect == null)
            {
                throw new InvalidOperationException("The reward collection is missing its scroll reference.");
            }
            if (!interactionEnabled)
            {
                StopScrolling();
            }
            // Disabling ScrollRect also cancels an active drag captured before a modal opened.
            _scrollRect.enabled = interactionEnabled;
        }

        private void ValidateReferences()
        {
            if (_scrollRect == null || _content == null || _itemPrefab == null || _emptyLabel == null
                || _scrollRect.content != _content)
            {
                throw new InvalidOperationException("A reward collection is missing its scroll, content, item, or empty label reference.");
            }
            _itemPrefab.ValidateReferences();
        }
    }
}
