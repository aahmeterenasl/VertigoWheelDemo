using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class AuthoredRewardBalanceTests
    {
        private GameSettings Game => AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");

        [Test]
        public void SharedBaseline_HasAllCatalogRewardsAndEnoughPermanentIdentities()
        {
            var game = Game;
            var pool = game.BronzeWheel.Pool;
            Assert.That(game.SilverWheel.Pool, Is.SameAs(pool));
            Assert.That(game.GoldenWheel.Pool, Is.SameAs(pool));
            Assert.That(game.BronzeWheel.QuantityMultiplier, Is.EqualTo(1));
            Assert.That(game.SilverWheel.QuantityMultiplier, Is.EqualTo(2));
            Assert.That(game.GoldenWheel.QuantityMultiplier, Is.EqualTo(10));
            var entries = Enumerable.Range(0, pool.EntryCount).Select(pool.GetEntry).ToArray();
            Assert.That(entries.Select(e => e.RewardSlot.Reward.RewardId).Distinct(),
                Is.EquivalentTo(Enum.GetValues(typeof(RewardId)).Cast<RewardId>().Where(id => id != RewardId.None)));
            Assert.That(entries.Where(e => e.MinimumTier == 0 && e.MaximumTier == -1 && e.PoolWeight > 0)
                .Select(e => e.RewardSlot.Reward.RewardId).Distinct().Count(), Is.GreaterThanOrEqualTo(8));
            Assert.That(entries.All(e => e.RewardSlot.LandingWeight == 1), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(11)]
        [TestCase(29)]
        [TestCase(30)]
        [TestCase(31)]
        [TestCase(60)]
        [TestCase(96)]
        [TestCase(199999981)]
        [TestCase(1000000000)]
        [TestCase(int.MaxValue)]
        public void AuthoredWheels_PrepareDistinctPositiveRewardsAcrossEligibilityConvergenceAndCapBoundaries(int zone)
        {
            var game = Game;
            for (int seed = 0; seed < 4; seed++)
            {
                var wheel = game.CreateWheelData(zone, new SystemRandomSource(seed));
                var rewards = wheel.Slots.Where(s => s.OutcomeKind == SlotOutcomeKind.Reward).ToArray();
                Assert.That(rewards.Select(s => (s.RewardId, s.Quantity)).Distinct().Count(), Is.EqualTo(rewards.Length));
                Assert.That(rewards.All(s => s.Quantity >= 1 && s.Quantity <= game.ZoneSettings.MaximumRewardQuantity), Is.True);
                Assert.That(wheel.Slots.All(s => s.LandingWeight == 1), Is.True);
                int bombs = wheel.Slots.Count(s => s.OutcomeKind == SlotOutcomeKind.Bomb);
                Assert.That(bombs, Is.EqualTo(wheel.ZoneType == ZoneType.Normal ? 1 : 0));
                Assert.That(rewards.Length, Is.EqualTo(bombs == 1 ? 7 : 8));
            }
        }

        [Test]
        public void GoldenOverride_PreservesTwoDistinctGoldVariantsWithOneMultiplier()
        {
            var game = Game;
            var random = new SequenceRandomSource(0);
            var wheel = game.CreateWheelData(30, random);
            Assert.That(random.SamplesConsumed, Is.Zero);
            Assert.That(wheel.Slots.Where(s => s.RewardId == RewardId.Gold).Select(s => s.Quantity),
                Is.EquivalentTo(new long[] { 1000, 10000 }));
            Assert.That(wheel.Slots.Where(s => s.RewardId != RewardId.Gold).All(s => s.Quantity == 10), Is.True);
        }

        [Test]
        public void SharedBaseline_CanStillFillEveryWheelWhenAllVariantsOfAnIdentityConverge()
        {
            var game = Object.Instantiate(Game);
            var zones = Object.Instantiate(game.ZoneSettings);
            try
            {
                game.ZoneSettings = zones;
                zones.MaximumRewardQuantity = 1;
                game.SetZoneOverrides(Array.Empty<ZoneWheelOverrideSettings>());
                foreach (int zone in new[] { 1, 5, 30, int.MaxValue })
                {
                    var wheel = game.CreateWheelData(zone, new SystemRandomSource(15));
                    var rewards = wheel.Slots.Where(s => s.OutcomeKind == SlotOutcomeKind.Reward).ToArray();
                    Assert.That(rewards.All(s => s.Quantity == 1), Is.True);
                    Assert.That(rewards.Select(s => s.RewardId).Distinct().Count(), Is.EqualTo(rewards.Length));
                }
                game.SetZoneOverrides(new[] { Game.GetZoneOverride(0) });
                Assert.Throws<ArgumentException>(() => game.Validate(), "A fixed override with collapsed Gold variants must report invalid authoring.");
            }
            finally { Object.DestroyImmediate(zones); Object.DestroyImmediate(game); }
        }

        [Test]
        public void MaximumAuthoredZone_CanResolveAndCashOutWithoutPreparingAnOverflowedZone()
        {
            var game = Game;
            var run = new RunSession(game, new SystemRandomSource(15), new ZeroRandom(), int.MaxValue);
            Assert.That(run.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(ticket.Outcome.OutcomeKind, Is.EqualTo(SlotOutcomeKind.Reward));
            Assert.That(run.TryResolveSpin(ticket), Is.True);
            Assert.That(run.TryCompleteResolution(ticket), Is.True);
            Assert.That(run.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(run.Summary.ZoneNumber, Is.EqualTo(int.MaxValue));
            Assert.That(run.Summary.Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
        }

        private sealed class ZeroRandom : IRandomSource { public double NextDouble() => 0; }
    }
}
