using System;
using VertigoWheel.Core.Rewards;

namespace VertigoWheel.Core.Wheels
{
    public sealed class WheelSlotData
    {
        private readonly SlotOutcomeKind _outcomeKind;
        private readonly RewardId _rewardId;
        private readonly long _quantity;

        private WheelSlotData(SlotOutcomeKind outcomeKind, RewardId rewardId, long quantity)
        {
            _outcomeKind = outcomeKind;
            _rewardId = rewardId;
            _quantity = quantity;
        }

        public SlotOutcomeKind OutcomeKind => _outcomeKind;
        public RewardId RewardId => _rewardId;
        public long Quantity => _quantity;

        public static WheelSlotData CreateReward(RewardId rewardId, long quantity)
        {
            if (rewardId == RewardId.None || !Enum.IsDefined(typeof(RewardId), rewardId))
            {
                throw new ArgumentOutOfRangeException(nameof(rewardId), "Select a defined reward other than None.");
            }

            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            return new WheelSlotData(SlotOutcomeKind.Reward, rewardId, quantity);
        }

        public static WheelSlotData CreateBomb()
        {
            return new WheelSlotData(SlotOutcomeKind.Bomb, RewardId.None, 0);
        }
    }
}
