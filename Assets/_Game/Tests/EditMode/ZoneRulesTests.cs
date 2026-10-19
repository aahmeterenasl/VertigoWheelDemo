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
        [TestCase(5, 0, 100)]
        [TestCase(6, 1, 125)]
        [TestCase(10, 1, 125)]
        [TestCase(11, 2, 150)]
        [TestCase(31, 6, 250)]
        public void RewardProgression_UsesZeroBasedTiers(int zoneNumber, int expectedTier, long expectedQuantity)
        {
            ZoneRules rules = CreateRules();

            Assert.That(rules.GetRewardTier(zoneNumber), Is.EqualTo(expectedTier));
            Assert.That(rules.GetRewardQuantity(zoneNumber, 100, 25), Is.EqualTo(expectedQuantity));
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

        private static ZoneRules CreateRules(CashOutPolicy cashOutPolicy = CashOutPolicy.AnyZone)
        {
            return new ZoneRules(5, 30, cashOutPolicy, 5, 1000);
        }
    }
}
