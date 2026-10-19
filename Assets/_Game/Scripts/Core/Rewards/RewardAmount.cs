using System;

namespace VertigoWheel.Core.Rewards
{
    public sealed class RewardAmount
    {
        private readonly RewardId _rewardId;
        private readonly long _quantity;

        public RewardAmount(RewardId rewardId, long quantity)
        {
            if (rewardId == RewardId.None || !Enum.IsDefined(typeof(RewardId), rewardId))
            {
                throw new ArgumentOutOfRangeException(nameof(rewardId), "Select a defined reward other than None.");
            }

            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            _rewardId = rewardId;
            _quantity = quantity;
        }

        public RewardId RewardId => _rewardId;
        public long Quantity => _quantity;
    }
}
