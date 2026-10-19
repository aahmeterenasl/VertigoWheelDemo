using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class RewardDistributionTests
    {
        [TestCase(ZoneType.Normal, 7)]
        [TestCase(ZoneType.Safe, 8)]
        [TestCase(ZoneType.Super, 8)]
        public void Generation_SelectsWithoutReplacementAndReservesTheCorrectBombCount(ZoneType type, int rewardCount)
        {
            SequenceRandomSource random = new SequenceRandomSource(0);
            WheelData wheel = new WheelGenerator().Generate(1, type, CreateEntries(), random, 3);

            Assert.That(wheel.Slots.Count(slot => slot.OutcomeKind == SlotOutcomeKind.Reward), Is.EqualTo(rewardCount));
            Assert.That(wheel.Slots.Where(slot => slot.OutcomeKind == SlotOutcomeKind.Reward)
                .Select(slot => (slot.RewardId, slot.Quantity)).Distinct().Count(), Is.EqualTo(rewardCount));
            Assert.That(random.SamplesConsumed, Is.EqualTo(rewardCount));
            if (type == ZoneType.Normal)
            {
                Assert.That(wheel.Slots[3].OutcomeKind, Is.EqualTo(SlotOutcomeKind.Bomb));
            }
        }

        [TestCase(0.0, 100)]
        [TestCase(0.299999, 100)]
        [TestCase(0.3, 101)]
        [TestCase(0.999999, 107)]
        public void PoolWeights_UseCumulativeIntervals(double sample, long expected)
        {
            List<RewardPoolEntryData> entries = CreateEntries();
            entries[0] = new RewardPoolEntryData(entries[0].Reward, 3);
            WheelData wheel = new WheelGenerator().Generate(5, ZoneType.Safe, entries, new SequenceRandomSource(sample));

            Assert.That(wheel.Slots[0].Quantity, Is.EqualTo(expected));
        }

        [Test]
        public void IdenticalRewardAmountEntries_AreAllRemovedAfterOneIsChosen()
        {
            List<RewardPoolEntryData> entries = CreateEntries();
            entries.Insert(1, new RewardPoolEntryData(WheelSlotData.CreateReward(RewardId.Gold, 100, 4), 10));
            WheelData wheel = new WheelGenerator().Generate(5, ZoneType.Safe, entries, new SequenceRandomSource(0));

            Assert.That(wheel.Slots.Count(slot => slot.Quantity == 100), Is.EqualTo(1));
            Assert.That(wheel.Slots.Select(slot => slot.Quantity), Is.EquivalentTo(Enumerable.Range(100, 8)));
        }

        [Test]
        public void DisabledEntries_AreExcludedAndTooFewDistinctOutcomesFailBeforeDrawing()
        {
            List<RewardPoolEntryData> entries = CreateEntries();
            entries.Add(new RewardPoolEntryData(WheelSlotData.CreateReward(RewardId.Gold, 1000), 0));
            WheelData wheel = new WheelGenerator().Generate(5, ZoneType.Safe, entries, new SequenceRandomSource(0.99999));
            Assert.That(wheel.Slots.Any(slot => slot.Quantity == 1000), Is.False);

            entries[1] = new RewardPoolEntryData(entries[0].Reward, 1);
            SequenceRandomSource random = new SequenceRandomSource(0);
            Assert.Throws<InvalidOperationException>(() => new WheelGenerator().Generate(5, ZoneType.Safe, entries, random));
            Assert.That(random.SamplesConsumed, Is.Zero);
        }

        [TestCase(0.0, 0)]
        [TestCase(0.299999, 0)]
        [TestCase(0.3, 1)]
        [TestCase(0.999999, 7)]
        public void LandingWeights_UseTheirOwnCumulativeIntervals(double sample, int expected)
        {
            WheelSlotData[] slots = CreateEntries().Select(entry => entry.Reward).ToArray();
            slots[0] = WheelSlotData.CreateReward(RewardId.Gold, 100, 3);
            WheelData wheel = new WheelData(5, ZoneType.Safe, slots);

            Assert.That(new OutcomeSelector(new SequenceRandomSource(sample)).SelectIndex(wheel), Is.EqualTo(expected));
        }

        [Test]
        public void EqualLandingWeights_GiveEverySlotAnEqualIntervalIncludingTheBomb()
        {
            WheelData wheel = new WheelGenerator().Generate(1, ZoneType.Normal, CreateEntries(), new SequenceRandomSource(0));
            for (int index = 0; index < WheelData.SlotCount; index++)
            {
                Assert.That(wheel.Slots[index].LandingWeight, Is.EqualTo(1));
                Assert.That(new OutcomeSelector(new SequenceRandomSource((index + 0.5) / 8)).SelectIndex(wheel), Is.EqualTo(index));
            }
        }

        [Test]
        public void LargeLandingWeights_DoNotOverflowTheirTotal()
        {
            WheelSlotData[] slots = Enumerable.Range(0, 8)
                .Select(index => WheelSlotData.CreateReward(RewardId.Gold, 100 + index, int.MaxValue)).ToArray();
            Assert.That(new OutcomeSelector(new SequenceRandomSource(0.5))
                .SelectIndex(new WheelData(5, ZoneType.Safe, slots)), Is.EqualTo(4));
        }

        [TestCase(-0.1)]
        [TestCase(1.0)]
        [TestCase(double.NaN)]
        public void InvalidRandomSamples_AreRejected(double sample)
        {
            WheelData wheel = new WheelData(5, ZoneType.Safe, CreateEntries().Select(entry => entry.Reward).ToArray());
            Assert.Throws<InvalidOperationException>(() => new OutcomeSelector(new SequenceRandomSource(sample)).SelectIndex(wheel));
        }

        [Test]
        public void FixedWheels_RejectRepeatedPairsButAllowDifferentAmountsAndDifferentRewardIds()
        {
            WheelSlotData[] slots = CreateEntries().Select(entry => entry.Reward).ToArray();
            slots[1] = slots[0];
            Assert.Throws<ArgumentException>(() => new WheelData(5, ZoneType.Safe, slots));
            slots[1] = WheelSlotData.CreateReward(RewardId.Cash, 100);
            Assert.DoesNotThrow(() => new WheelData(5, ZoneType.Safe, slots));
            slots[1] = WheelSlotData.CreateReward(RewardId.Gold, 1000);
            Assert.DoesNotThrow(() => new WheelData(5, ZoneType.Safe, slots));
        }

        [Test]
        public void InvalidWeightsAndBombPoolEntries_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSlotData.CreateReward(RewardId.Cash, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSlotData.CreateBomb(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardPoolEntryData(WheelSlotData.CreateReward(RewardId.Cash, 1), -1));
            Assert.Throws<ArgumentException>(() => new RewardPoolEntryData(WheelSlotData.CreateBomb(), 1));
        }

        [Test]
        public void SeededSources_ReproduceGeneratedContentAndLanding()
        {
            SystemRandomSource firstRandom = new SystemRandomSource(17);
            SystemRandomSource secondRandom = new SystemRandomSource(17);
            WheelData first = new WheelGenerator().Generate(1, ZoneType.Normal, CreateEntries(), firstRandom);
            WheelData second = new WheelGenerator().Generate(1, ZoneType.Normal, CreateEntries(), secondRandom);

            Assert.That(first.Slots.Select(slot => slot.Quantity), Is.EqualTo(second.Slots.Select(slot => slot.Quantity)));
            Assert.That(new OutcomeSelector(firstRandom).SelectIndex(first), Is.EqualTo(new OutcomeSelector(secondRandom).SelectIndex(second)));
        }

        private static List<RewardPoolEntryData> CreateEntries()
        {
            return Enumerable.Range(100, 8)
                .Select(quantity => new RewardPoolEntryData(WheelSlotData.CreateReward(RewardId.Gold, quantity), 1)).ToList();
        }
    }
}
