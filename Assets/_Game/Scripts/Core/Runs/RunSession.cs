using System;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Core.Runs
{
    /// <summary>
    /// Owns one run on the game thread. Expected duplicate/out-of-state commands
    /// return false; invalid authored data throws before a transition is committed.
    /// </summary>
    public sealed class RunSession
    {
        private readonly IRunSettingsSource _settings;
        private readonly IRandomSource _wheelRandom;
        private readonly OutcomeSelector _outcomeSelector;
        private readonly RewardLedger _ledger = new RewardLedger();
        private long _runGeneration;
        private long _spinSequence;
        private RunState _state;
        private WheelData _wheel;
        private SpinTicket _pendingSpin;
        private RunSummary _summary;

        /// <param name="initialZoneNumber">Defaults to 1; allows developer checks at specific zone boundaries.</param>
        public RunSession(
            IRunSettingsSource settings,
            IRandomSource wheelRandom,
            IRandomSource landingRandom,
            int initialZoneNumber = 1)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _wheelRandom = wheelRandom ?? throw new ArgumentNullException(nameof(wheelRandom));
            _outcomeSelector = new OutcomeSelector(landingRandom);
            StartNewRun(initialZoneNumber);
        }

        public long RunGeneration => _runGeneration;
        public RunState State => _state;
        public int ZoneNumber => _wheel.ZoneNumber;
        public WheelData Wheel => _wheel;
        public SpinTicket PendingSpin => _pendingSpin;
        public RunSummary Summary => _summary;
        public bool CanSpin => _state == RunState.Ready;

        // Policy changes apply immediately, but the displayed wheel's classification
        // stays fixed until the next zone, just like its contents and quantities.
        public bool CanCashOut => _state == RunState.Ready
            && ReadRules().AllowsCashOut(_wheel.ZoneType);

        public RunSnapshot CreateSnapshot()
        {
            return new RunSnapshot(
                _runGeneration, _state, _wheel, _pendingSpin,
                _ledger.CreateSnapshot(), _summary, CanCashOut);
        }

        public bool TryStartSpin(out SpinTicket ticket)
        {
            ticket = null;
            if (!CanSpin)
            {
                return false;
            }

            long sequence = checked(_spinSequence + 1);
            int index = _outcomeSelector.SelectIndex(_wheel);
            ticket = new SpinTicket(_runGeneration, sequence, _wheel, index);
            _spinSequence = sequence;
            _pendingSpin = ticket;
            _state = RunState.Spinning;
            return true;
        }

        public bool TryResolveSpin(SpinTicket ticket)
        {
            if (!IsPending(ticket, RunState.Spinning))
            {
                return false;
            }

            if (ticket.Outcome.OutcomeKind == SlotOutcomeKind.Bomb)
            {
                _ledger.Clear();
            }
            else
            {
                _ledger.Add(new RewardAmount(ticket.Outcome.RewardId, ticket.Outcome.Quantity));
            }

            _state = RunState.Resolving;
            return true;
        }

        public bool TryCompleteResolution(SpinTicket ticket)
        {
            if (!IsPending(ticket, RunState.Resolving))
            {
                return false;
            }

            if (ticket.Outcome.OutcomeKind == SlotOutcomeKind.Bomb)
            {
                _pendingSpin = null;
                _state = RunState.Failed;
                return true;
            }

            // The integer zone ceiling completes the run rather than wrapping or
            // leaving the acknowledged reward stuck in Resolving.
            if (_wheel.ZoneNumber == int.MaxValue)
            {
                CompleteCashOut();
                return true;
            }

            int nextZone = _wheel.ZoneNumber + 1;
            WheelData nextWheel = PrepareWheel(nextZone);
            _wheel = nextWheel;
            _pendingSpin = null;
            _state = RunState.Ready;
            return true;
        }

        public bool TryCashOut(out RunSummary summary)
        {
            summary = null;
            if (!CanCashOut)
            {
                return false;
            }

            CompleteCashOut();
            summary = _summary;
            return true;
        }

        public void Restart()
        {
            StartNewRun(1);
        }

        private void CompleteCashOut()
        {
            RunSummary summary = new RunSummary(_runGeneration, _wheel.ZoneNumber, _ledger.CreateSnapshot());
            _ledger.Clear();
            _summary = summary;
            _pendingSpin = null;
            _state = RunState.CashedOut;
        }

        private void StartNewRun(int initialZoneNumber)
        {
            long generation = checked(_runGeneration + 1);
            WheelData firstWheel = PrepareWheel(initialZoneNumber);
            _ledger.Clear();
            _wheel = firstWheel;
            _runGeneration = generation;
            _spinSequence = 0;
            _pendingSpin = null;
            _summary = null;
            _state = RunState.Ready;
        }

        private bool IsPending(SpinTicket ticket, RunState expectedState)
        {
            return _state == expectedState && ticket != null && ReferenceEquals(ticket, _pendingSpin);
        }

        private ZoneRules ReadRules()
        {
            return _settings.CreateZoneRules()
                ?? throw new InvalidOperationException("The run settings source returned no zone rules.");
        }

        private WheelData PrepareWheel(int zoneNumber)
        {
            ZoneRules rules = ReadRules();
            ZoneType expectedType = rules.GetZoneType(zoneNumber);
            WheelData wheel = _settings.CreateWheelData(zoneNumber, _wheelRandom);
            if (wheel == null || wheel.ZoneNumber != zoneNumber || wheel.ZoneType != expectedType)
            {
                throw new InvalidOperationException("The run settings source returned a wheel for a different zone or type.");
            }
            return wheel;
        }
    }
}
