using System;
using System.Collections.Generic;

namespace VertigoWheel.Core.Rewards
{
    /// <summary>
    /// Aggregates rewards in first-earned order. Totals saturate at long.MaxValue
    /// so an unlimited run cannot overflow a reward quantity.
    /// </summary>
    internal sealed class RewardLedger
    {
        private readonly List<RewardAmount> _rewards = new List<RewardAmount>();
        private readonly Dictionary<RewardId, int> _indices = new Dictionary<RewardId, int>();

        internal void Add(RewardAmount reward)
        {
            if (reward == null)
            {
                throw new ArgumentNullException(nameof(reward));
            }

            if (_indices.TryGetValue(reward.RewardId, out int index))
            {
                long currentQuantity = _rewards[index].Quantity;
                long quantity = reward.Quantity > long.MaxValue - currentQuantity
                    ? long.MaxValue
                    : currentQuantity + reward.Quantity;

                _rewards[index] = new RewardAmount(reward.RewardId, quantity);
                return;
            }

            _indices.Add(reward.RewardId, _rewards.Count);
            _rewards.Add(reward);
        }

        internal void Clear()
        {
            _rewards.Clear();
            _indices.Clear();
        }

        internal IReadOnlyList<RewardAmount> CreateSnapshot()
        {
            return new List<RewardAmount>(_rewards).AsReadOnly();
        }
    }
}
