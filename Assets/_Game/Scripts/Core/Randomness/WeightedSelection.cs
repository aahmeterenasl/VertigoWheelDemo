using System;
using System.Collections.Generic;

namespace VertigoWheel.Core.Randomness
{
    internal static class WeightedSelection
    {
        internal static int SelectIndex(IReadOnlyList<int> weights, IRandomSource random)
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            long total = 0;
            int lastPositiveIndex = -1;
            for (int index = 0; index < weights.Count; index++)
            {
                if (weights[index] < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(weights));
                }

                total += weights[index];
                if (weights[index] > 0)
                {
                    lastPositiveIndex = index;
                }
            }

            if (total == 0)
            {
                throw new ArgumentException("At least one weight must be positive.", nameof(weights));
            }

            double sample = random.NextDouble();
            if (double.IsNaN(sample) || sample < 0 || sample >= 1)
            {
                throw new InvalidOperationException("The random source must return a value in [0, 1).");
            }

            double target = sample * total;
            long cumulative = 0;
            for (int index = 0; index < weights.Count; index++)
            {
                cumulative += weights[index];
                if (target < cumulative)
                {
                    return index;
                }
            }

            // A product very close to total can round up; keep it in the last positive interval.
            return lastPositiveIndex;
        }
    }
}
