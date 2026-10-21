using System;
using NUnit.Framework;
using VertigoWheel.UI.Common;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class QuantityFormatterTests
    {
        [TestCase(0L, "0")]
        [TestCase(999L, "999")]
        [TestCase(1463L, "1,463")]
        [TestCase(999999L, "999,999")]
        [TestCase(1000000L, "1M")]
        [TestCase(1234567L, "1.2M")]
        [TestCase(999999999L, "999M")]
        [TestCase(1000000000L, "1B")]
        [TestCase(1000000001L, "1B")]
        [TestCase(999999999999L, "999B")]
        [TestCase(1000000000000L, "1T")]
        [TestCase(1000000000001L, "1T")]
        [TestCase(999999999999999L, "999T")]
        [TestCase(1000000000000000L, "1Qa")]
        [TestCase(1000000000000001L, "1Qa")]
        [TestCase(999999999999999999L, "999Qa")]
        [TestCase(1000000000000000000L, "1Qi")]
        [TestCase(1000000000000000001L, "1Qi")]
        [TestCase(long.MaxValue - 1L, "9.2Qi")]
        [TestCase(long.MaxValue, "9.2Qi")]
        public void Display_PreservesOrdinaryTotalsAndCompactsLargeValues(long value, string expected) =>
            Assert.That(QuantityFormatter.Format(value), Is.EqualTo(expected));

        [TestCase(999L, "999")]
        [TestCase(1000L, "1K")]
        [TestCase(1001L, "1K")]
        [TestCase(1999L, "1.9K")]
        [TestCase(99999L, "99.9K")]
        [TestCase(100000L, "100K")]
        [TestCase(999999L, "999K")]
        [TestCase(1000000L, "1M")]
        [TestCase(1000001L, "1M")]
        [TestCase(999999999999999999L, "999Qa")]
        [TestCase(99999999999999999L, "99.9Qa")]
        [TestCase(long.MaxValue, "9.2Qi")]
        public void Compact_TruncatesWithoutRoundingAcrossUnitBoundaries(long value, string expected) =>
            Assert.That(QuantityFormatter.Format(value, true), Is.EqualTo(expected));

        [Test]
        public void NegativeAmount_IsRejected() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormatter.Format(-1));
    }
}
