using System;

namespace VertigoWheel.Core.Wheels
{
    public sealed class RewardPoolEntryData
    {
        private readonly WheelSlotData _reward;
        private readonly int _poolWeight;

        public RewardPoolEntryData(WheelSlotData reward, int poolWeight)
        {
            if (reward == null)
            {
                throw new ArgumentNullException(nameof(reward));
            }

            if (reward.OutcomeKind != SlotOutcomeKind.Reward)
            {
                throw new ArgumentException("Bombs are not reward-pool entries.", nameof(reward));
            }

            if (poolWeight < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(poolWeight));
            }

            _reward = reward;
            _poolWeight = poolWeight;
        }

        public WheelSlotData Reward => _reward;
        public int PoolWeight => _poolWeight;
    }
}
