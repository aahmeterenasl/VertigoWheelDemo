using System;
using NUnit.Framework;
using VertigoWheel.Core.Rewards;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class RewardAmountTests
    {
        [TestCase(RewardId.None)]
        [TestCase((RewardId)(-1))]
        [TestCase((RewardId)999)]
        public void InvalidRewardIdentity_IsRejected(RewardId rewardId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardAmount(rewardId, 1));
        }
        [TestCase(0L)]
        [TestCase(-1L)]
        public void NonPositiveQuantity_IsRejected(long quantity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardAmount(RewardId.Cash, quantity));
        }

    }
}
