using System;

namespace VertigoWheel.Core.Wheels
{
    /// <summary>Wheel angles start at up; positive angles rotate counterclockwise.</summary>
    public static class WheelAngleCalculator
    {
        // Keeping unwrapped rotations below this limit preserves sub-degree precision in double.
        // A caller can rebase its rotation between animations instead of accumulating indefinitely.
        private const double MaximumAbsoluteRotationDegrees = 1e12;
        private const double DegreesPerTurn = 360;

        /// <summary>Returns a slot's local angle normalized to [0, 360).</summary>
        public static double GetSlotAngleDegrees(
            int slotIndex,
            double slotZeroAngleDegrees,
            bool clockwiseSlotOrder)
        {
            ValidateSlotIndex(slotIndex);
            ValidateFiniteAngle(slotZeroAngleDegrees, nameof(slotZeroAngleDegrees));

            double slotStep = DegreesPerTurn / WheelData.SlotCount;
            double direction = clockwiseSlotOrder ? -1 : 1;
            return Normalize(Normalize(slotZeroAngleDegrees) + direction * slotIndex * slotStep);
        }

        /// <summary>
        /// Returns an unwrapped rotation that places the selected slot under the pointer after
        /// at least the requested complete turns in the spin direction. Unwrapped rotations must
        /// remain within +/- 1e12 degrees to retain useful angular precision.
        /// </summary>
        public static double GetTargetRotationDegrees(
            double currentRotationDegrees,
            int selectedSlotIndex,
            double slotZeroAngleDegrees,
            bool clockwiseSlotOrder,
            double pointerAngleDegrees,
            int fullTurns,
            bool clockwiseSpin = true)
        {
            ValidateFiniteAngle(currentRotationDegrees, nameof(currentRotationDegrees));
            ValidateFiniteAngle(pointerAngleDegrees, nameof(pointerAngleDegrees));
            if (Math.Abs(currentRotationDegrees) > MaximumAbsoluteRotationDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentRotationDegrees), "Rebase the rotation before calculating another spin.");
            }

            if (fullTurns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fullTurns));
            }

            double slotAngle = GetSlotAngleDegrees(selectedSlotIndex, slotZeroAngleDegrees, clockwiseSlotOrder);
            double targetAngle = Normalize(Normalize(pointerAngleDegrees) - slotAngle);
            double currentAngle = Normalize(currentRotationDegrees);
            double remainingAngle = clockwiseSpin
                ? Normalize(currentAngle - targetAngle)
                : Normalize(targetAngle - currentAngle);
            double distance = fullTurns * DegreesPerTurn + remainingAngle;
            double target = currentRotationDegrees + (clockwiseSpin ? -distance : distance);

            if (double.IsNaN(target) || double.IsInfinity(target) ||
                Math.Abs(target) > MaximumAbsoluteRotationDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentRotationDegrees), "Rebase the rotation before calculating this spin.");
            }

            return target;
        }

        private static double Normalize(double angleDegrees)
        {
            double normalized = angleDegrees % DegreesPerTurn;
            if (normalized < 0)
            {
                normalized += DegreesPerTurn;
            }

            return normalized == DegreesPerTurn ? 0 : normalized;
        }

        private static void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= WheelData.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
        }

        private static void ValidateFiniteAngle(double angleDegrees, string parameterName)
        {
            if (double.IsNaN(angleDegrees) || double.IsInfinity(angleDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
