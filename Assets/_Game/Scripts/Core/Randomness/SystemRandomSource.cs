using System;

namespace VertigoWheel.Core.Randomness
{
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource()
        {
            _random = new Random();
        }

        public SystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public double NextDouble()
        {
            return _random.NextDouble();
        }
    }
}
