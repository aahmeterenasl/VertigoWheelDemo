using System;
using NUnit.Framework;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class ZoneRulesTests
    {
        [TestCase(1, ZoneType.Normal)]
        [TestCase(4, ZoneType.Normal)]
        [TestCase(5, ZoneType.Safe)]
        [TestCase(6, ZoneType.Normal)]
        [TestCase(29, ZoneType.Normal)]
        [TestCase(30, ZoneType.Super)]
        [TestCase(31, ZoneType.Normal)]
        [TestCase(60, ZoneType.Super)]
        public void GetZoneType_ClassifiesZoneBoundaries(int zoneNumber, ZoneType expected)
        {
            ZoneRules rules = CreateRules();

            Assert.That(rules.GetZoneType(zoneNumber), Is.EqualTo(expected));
        }

        [Test]
        public void GetZoneType_UsesAuthoredIntervalsAndSuperPriority()
        {
            ZoneRules rules = new ZoneRules(3, 6, CashOutPolicy.AnyZone, 4, 1000);

            Assert.That(rules.GetZoneType(3), Is.EqualTo(ZoneType.Safe));
            Assert.That(rules.GetZoneType(6), Is.EqualTo(ZoneType.Super));
            Assert.That(rules.GetZoneType(5), Is.EqualTo(ZoneType.Normal));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(30)]
        public void AnyZone_AllowsCashOutAtEveryZone(int zoneNumber)
        {
            Assert.That(CreateRules().AllowsCashOut(zoneNumber), Is.True);
        }

        [TestCase(1, false)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(30, true)]
        [TestCase(31, false)]
        public void RestrictedPolicy_UsesTheDisplayedZone(int zoneNumber, bool expected)
        {
            ZoneRules rules = CreateRules(CashOutPolicy.SafeAndSuperZonesOnly);

            Assert.That(rules.AllowsCashOut(zoneNumber), Is.EqualTo(expected));
        }

        [TestCase(1, 0, 100)]
        [TestCase(2, 0, 125)]
        [TestCase(5, 0, 200)]
        [TestCase(6, 1, 225)]
        [TestCase(10, 1, 325)]
        [TestCase(11, 2, 350)]
        [TestCase(31, 6, 850)]
        public void RewardProgression_GrowsEveryZoneWhileEligibilityUsesZeroBasedTiers(int zoneNumber, int expectedTier, long expectedQuantity)
        {
            ZoneRules rules = CreateRules();

            Assert.That(rules.GetRewardTier(zoneNumber), Is.EqualTo(expectedTier));
            Assert.That(rules.GetRewardQuantity(zoneNumber, 100, 25), Is.EqualTo(expectedQuantity));
        }

        [TestCase(1, 1, 100)]
        [TestCase(2, 2, 210)]
        [TestCase(5, 2, 240)]
        [TestCase(5, 5, 600)]
        [TestCase(30, 10, 2450)]
        public void RewardMultiplier_AppliesAfterEveryZoneGrowth(int zone, int multiplier, long expected)
        {
            var rules = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, 1000000);
            Assert.That(rules.GetRewardQuantity(zone, 100, 5, multiplier), Is.EqualTo(expected));
        }

        [Test]
        public void QuantityGrowth_DoesNotDependOnPoolEligibilityCadence()
        {
            var slowEligibility = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, 1000000);
            var fastEligibility = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 2, 1000000);
            Assert.That(slowEligibility.GetRewardTier(5), Is.Zero);
            Assert.That(fastEligibility.GetRewardTier(5), Is.EqualTo(2));
            Assert.That(slowEligibility.GetRewardQuantity(5, 100, 5, 2), Is.EqualTo(240));
            Assert.That(fastEligibility.GetRewardQuantity(5, 100, 5, 2), Is.EqualTo(240));
            Assert.That(slowEligibility.GetRewardQuantity(int.MaxValue, 7, 0, 5), Is.EqualTo(35));
        }

        [Test]
        public void QuantitySaturation_MatchesUnboundedArithmeticAtIntegerLimits()
        {
            foreach (long cap in new[] { 1L, 1000L, long.MaxValue })
            {
                var rules = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, cap);
                foreach (int zone in new[] { 1, 2, 30, int.MaxValue })
                foreach (long initial in new[] { 1L, Math.Max(1, cap - 1), cap, long.MaxValue })
                foreach (long growth in new[] { 0L, 1L, long.MaxValue / 2, long.MaxValue })
                foreach (int multiplier in new[] { 1, 2, 5, 10, int.MaxValue })
                {
                    var unbounded = ((System.Numerics.BigInteger)initial + (System.Numerics.BigInteger)(zone - 1) * growth) * multiplier;
                    long expected = (long)System.Numerics.BigInteger.Min(cap, unbounded);
                    Assert.That(rules.GetRewardQuantity(zone, initial, growth, multiplier), Is.EqualTo(expected),
                        $"zone={zone}, base={initial}, growth={growth}, multiplier={multiplier}, cap={cap}");
                }
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RewardQuantity_RejectsInvalidMultipliers(int multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRules().GetRewardQuantity(1, 100, 5, multiplier));
        }

        [Test]
        public void RewardQuantity_ClampsWithoutOverflowing()
        {
            ZoneRules rules = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 1, long.MaxValue);

            Assert.That(rules.GetRewardQuantity(int.MaxValue, 1, long.MaxValue), Is.EqualTo(long.MaxValue));
            Assert.That(rules.GetRewardQuantity(2, long.MaxValue - 1, 1), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void RewardQuantity_ClampsBaseQuantityAndSupportsNoGrowth()
        {
            ZoneRules rules = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, 200);

            Assert.That(rules.GetRewardQuantity(1, 500, 0), Is.EqualTo(200));
            Assert.That(rules.GetRewardQuantity(500, 50, 0), Is.EqualTo(50));
            Assert.That(rules.GetRewardQuantity(26, 100, 25), Is.EqualTo(200));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ZoneOperations_RejectNonPositiveZoneNumbers(int zoneNumber)
        {
            ZoneRules rules = CreateRules();

            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetZoneType(zoneNumber));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.AllowsCashOut(zoneNumber));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetRewardTier(zoneNumber));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetRewardQuantity(zoneNumber, 1, 1));
        }

        [Test]
        public void InvalidAuthoredRules_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneRules(0, 30, CashOutPolicy.AnyZone, 5, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneRules(5, 0, CashOutPolicy.AnyZone, 5, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneRules(5, 30, (CashOutPolicy)99, 5, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneRules(5, 30, CashOutPolicy.AnyZone, 0, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, 0));
        }

        [Test]
        public void RewardQuantity_RejectsInvalidAmounts()
        {
            ZoneRules rules = CreateRules();

            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetRewardQuantity(1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetRewardQuantity(1, 1, -1));
        }

        [TestCase(ZoneType.Normal, false)]
        [TestCase(ZoneType.Safe, true)]
        [TestCase(ZoneType.Super, true)]
        public void CashOutPolicy_CanEvaluateAnAlreadyPreparedWheelType(ZoneType zoneType, bool allowed)
        {
            ZoneRules rules = CreateRules(CashOutPolicy.SafeAndSuperZonesOnly);
            Assert.That(rules.AllowsCashOut(zoneType), Is.EqualTo(allowed));
        }

        [Test]
        public void UndefinedPreparedZoneType_IsRejectedEvenWithAnyZonePolicy()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRules().AllowsCashOut((ZoneType)999));
        }

        private static ZoneRules CreateRules(CashOutPolicy cashOutPolicy = CashOutPolicy.AnyZone)
        {
            return new ZoneRules(5, 30, cashOutPolicy, 5, 1000);
        }
    }
}
