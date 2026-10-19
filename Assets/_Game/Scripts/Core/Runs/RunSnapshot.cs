using System.Collections.Generic;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;

namespace VertigoWheel.Core.Runs
{
    /// <summary>Read-only session data captured for a view update.</summary>
    public sealed class RunSnapshot
    {
        private readonly long _runGeneration;
        private readonly RunState _state;
        private readonly WheelData _wheel;
        private readonly SpinTicket _pendingSpin;
        private readonly IReadOnlyList<RewardAmount> _rewards;
        private readonly RunSummary _summary;
        private readonly bool _canCashOut;

        internal RunSnapshot(
            long runGeneration,
            RunState state,
            WheelData wheel,
            SpinTicket pendingSpin,
            IReadOnlyList<RewardAmount> rewards,
            RunSummary summary,
            bool canCashOut)
        {
            _runGeneration = runGeneration;
            _state = state;
            _wheel = wheel;
            _pendingSpin = pendingSpin;
            _rewards = rewards;
            _summary = summary;
            _canCashOut = canCashOut;
        }

        public long RunGeneration => _runGeneration;
        public RunState State => _state;
        public int ZoneNumber => _wheel.ZoneNumber;
        public WheelData Wheel => _wheel;
        public SpinTicket PendingSpin => _pendingSpin;
        public IReadOnlyList<RewardAmount> Rewards => _rewards;
        public RunSummary Summary => _summary;
        public bool CanSpin => _state == RunState.Ready;
        public bool CanCashOut => _canCashOut;
    }
}
