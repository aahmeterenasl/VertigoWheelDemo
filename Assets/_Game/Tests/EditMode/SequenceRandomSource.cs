using System;
using VertigoWheel.Core.Randomness;

namespace VertigoWheel.Tests.EditMode
{
    internal sealed class SequenceRandomSource : IRandomSource
    {
        private readonly double[] _samples;
        private int _samplesConsumed;

        internal SequenceRandomSource(params double[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("Supply at least one deterministic sample.", nameof(samples));
            }

            _samples = (double[])samples.Clone();
        }

        internal int SamplesConsumed => _samplesConsumed;

        public double NextDouble()
        {
            int index = _samples.Length == 1 ? 0 : _samplesConsumed;
            _samplesConsumed++;
            return _samples[index];
        }
    }
}
