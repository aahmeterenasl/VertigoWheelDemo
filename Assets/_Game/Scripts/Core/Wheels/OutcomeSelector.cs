using System;
using VertigoWheel.Core.Randomness;

namespace VertigoWheel.Core.Wheels
{
    public sealed class OutcomeSelector
    {
        private readonly IRandomSource _random;

        public OutcomeSelector(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int SelectIndex(WheelData wheel)
        {
            if (wheel == null)
            {
                throw new ArgumentNullException(nameof(wheel));
            }

            int[] weights = new int[wheel.Slots.Count];
            for (int index = 0; index < weights.Length; index++)
            {
                weights[index] = wheel.Slots[index].LandingWeight;
            }

            return WeightedSelection.SelectIndex(weights, _random);
        }
    }
}
