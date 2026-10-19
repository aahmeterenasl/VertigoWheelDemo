using System.Collections.Generic;
using VertigoWheel.Core.Rewards;

namespace VertigoWheel.Core.Runs
{
    /// <summary>A detached cash-out result. Reading or animating it never grants rewards.</summary>
    public sealed class RunSummary
    {
        private readonly long _runGeneration;
        private readonly int _zoneNumber;
        private readonly IReadOnlyList<RewardAmount> _rewards;

        internal RunSummary(long runGeneration, int zoneNumber, IReadOnlyList<RewardAmount> rewards)
        {
            _runGeneration = runGeneration;
            _zoneNumber = zoneNumber;
            _rewards = rewards;
        }

        public long RunGeneration => _runGeneration;
        public int ZoneNumber => _zoneNumber;
        public IReadOnlyList<RewardAmount> Rewards => _rewards;
    }
}
