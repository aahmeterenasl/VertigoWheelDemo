using System;
using NUnit.Framework;
using VertigoWheel.Core.Wheels;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class WheelAngleCalculatorTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void SlotAngles_MatchEverySlotAndTheConfiguredOrder(bool clockwiseSlotOrder)
        {
            for (int index = 0; index < WheelData.SlotCount; index++)
            {
                double expected = Normalize(22.5 + index * (clockwiseSlotOrder ? -45 : 45));

                Assert.That(WheelAngleCalculator.GetSlotAngleDegrees(index, 22.5, clockwiseSlotOrder),
                    Is.EqualTo(expected).Within(1e-9), $"Slot {index}");
            }
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void Targets_AlignEverySlotAndPreserveDirectionAndFullTurns(
            bool clockwiseSlotOrder, bool clockwiseSpin)
        {
            double[] previousRotations = { 0, 43.25, -81.5, 360 * 12 + 33.75, -360 * 17 - 19.25 };
            foreach (double currentRotation in previousRotations)
            {
                for (int slotIndex = 0; slotIndex < WheelData.SlotCount; slotIndex++)
                {
                    double target = WheelAngleCalculator.GetTargetRotationDegrees(
                        currentRotation, slotIndex, 22.5, clockwiseSlotOrder, -15.25, 4, clockwiseSpin);
                    double slotAngle = 22.5 + slotIndex * (clockwiseSlotOrder ? -45 : 45);
                    double traveled = clockwiseSpin ? currentRotation - target : target - currentRotation;

                    Assert.That(Normalize(slotAngle + target), Is.EqualTo(Normalize(-15.25)).Within(1e-9),
                        $"Slot {slotIndex} from rotation {currentRotation}");
                    Assert.That(traveled, Is.GreaterThanOrEqualTo(4 * 360));
                    Assert.That(traveled, Is.LessThan(5 * 360));
                }
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AlreadyAlignedTargets_MakeExactlyTheRequestedFullTurns(bool clockwiseSpin)
        {
            // Slot 3 is at -112.5 degrees; adding this rotation places it at pointer 17.5.
            double current = 130 + (clockwiseSpin ? -720 : 720);

            double target = WheelAngleCalculator.GetTargetRotationDegrees(
                current, 3, 22.5, true, 17.5, 3, clockwiseSpin);

            Assert.That(target, Is.EqualTo(current + (clockwiseSpin ? -1080 : 1080)));
        }

        [Test]
        public void DefaultSpinDirection_IsClockwise()
        {
            Assert.That(WheelAngleCalculator.GetTargetRotationDegrees(0, 0, 0, true, 0, 1),
                Is.EqualTo(-360));
        }

        [Test]
        public void EquivalentCalibrationAndPointerTurns_DoNotChangeTheResult()
        {
            double baseline = WheelAngleCalculator.GetTargetRotationDegrees(52.5, 6, 22.5, true, 30, 4);

            Assert.That(WheelAngleCalculator.GetTargetRotationDegrees(52.5, 6, 22.5 - 720, true, 30 + 1080, 4),
                Is.EqualTo(baseline));
        }

        [Test]
        public void LargeFiniteCalibrationAngles_AreNormalizedBeforeAddingSlotOffsets()
        {
            double offset = double.MaxValue;
            double pointer = -double.MaxValue;
            double target = WheelAngleCalculator.GetTargetRotationDegrees(0, 7, offset, false, pointer, 2);
            double slotAngle = WheelAngleCalculator.GetSlotAngleDegrees(7, offset, false);

            Assert.That(slotAngle, Is.EqualTo(Normalize(Normalize(offset) + 315)));
            Assert.That(double.IsNaN(target) || double.IsInfinity(target), Is.False);
            Assert.That(Normalize(slotAngle + target), Is.EqualTo(Normalize(pointer)).Within(1e-9));
        }

        [Test]
        public void MaximumTurnCount_DoesNotOverflowIntegerMultiplication()
        {
            double target = WheelAngleCalculator.GetTargetRotationDegrees(0, 0, 0, true, 0, int.MaxValue);

            Assert.That(target, Is.EqualTo(-360d * int.MaxValue));
            Assert.That(Normalize(target), Is.Zero);
        }

        [Test]
        public void ExcessiveAccumulatedRotations_AreRejectedInsteadOfLosingAngularPrecision()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetTargetRotationDegrees(double.MaxValue, 0, 0, true, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetTargetRotationDegrees(-double.MaxValue, 0, 0, true, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetTargetRotationDegrees(-1e12, 0, 0, true, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetTargetRotationDegrees(1e12, 0, 0, true, 0, 1, false));
        }

        [TestCase(-1)]
        [TestCase(8)]
        public void InvalidSlotIndices_AreRejected(int slotIndex)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetSlotAngleDegrees(slotIndex, 0, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetTargetRotationDegrees(0, slotIndex, 0, true, 0, 1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void MissingFullTurns_AreRejected(int fullTurns)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WheelAngleCalculator.GetTargetRotationDegrees(0, 0, 0, true, 0, fullTurns));
        }

        [Test]
        public void NonFiniteAngles_AreRejectedAtEveryInput()
        {
            double[] invalidAngles = { double.NaN, double.PositiveInfinity, double.NegativeInfinity };
            foreach (double angle in invalidAngles)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    WheelAngleCalculator.GetSlotAngleDegrees(0, angle, true));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    WheelAngleCalculator.GetTargetRotationDegrees(angle, 0, 0, true, 0, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    WheelAngleCalculator.GetTargetRotationDegrees(0, 0, angle, true, 0, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    WheelAngleCalculator.GetTargetRotationDegrees(0, 0, 0, true, angle, 1));
            }
        }

        private static double Normalize(double angleDegrees)
        {
            double normalized = angleDegrees % 360;
            return normalized < 0 ? normalized + 360 : normalized;
        }
    }
}
