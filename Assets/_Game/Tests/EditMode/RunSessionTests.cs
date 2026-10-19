using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class RunSessionTests
    {
        [Test]
        public void PreparedWheel_IsCachedAcrossSnapshotsEligibilityChecksAndSpinStart()
        {
            FakeSettingsSource source = new FakeSettingsSource();
            SequenceRandomSource generationRandom = new SequenceRandomSource(0);
            SequenceRandomSource landingRandom = new SequenceRandomSource(0.3);
            RunSession session = new RunSession(source, generationRandom, landingRandom);
            WheelData preparedWheel = session.Wheel;
            source.SetReward(RewardId.Gold, 50);

            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(session.CanSpin, Is.True);
            Assert.That(session.CanCashOut, Is.True);
            Assert.That(session.CreateSnapshot().Wheel, Is.SameAs(preparedWheel));
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(ticket.Wheel, Is.SameAs(preparedWheel));
            Assert.That(ticket.Wheel.Slots[0].RewardId, Is.EqualTo(RewardId.Cash));
            Assert.That(ticket.Wheel.Slots[0].Quantity, Is.EqualTo(10));
            Assert.That(ticket.SelectedSlotIndex, Is.EqualTo(2));
            Assert.That(session.TryStartSpin(out SpinTicket duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
            Assert.That(session.CreateSnapshot().Wheel, Is.SameAs(preparedWheel));
            Assert.That(source.WheelCreationCount, Is.EqualTo(1));
            Assert.That(generationRandom.SamplesConsumed, Is.EqualTo(1));
            Assert.That(landingRandom.SamplesConsumed, Is.EqualTo(1));
        }

        [Test]
        public void Reward_SettlesOnceBeforeAcknowledgmentAndAdvancesOnlyOnContinue()
        {
            RunSession session = CreateSession(new FakeSettingsSource());
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryCashOut(out _), Is.False);
            Assert.That(session.State, Is.EqualTo(RunState.Spinning));
            Assert.That(session.TryCompleteResolution(ticket), Is.False);
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);

            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Resolving));
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.CanSpin, Is.False);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryStartSpin(out _), Is.False);
            Assert.That(session.TryCashOut(out _), Is.False);
            Assert.That(session.TryResolveSpin(ticket), Is.False);
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));

            Assert.That(session.TryCompleteResolution(ticket), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.ZoneNumber, Is.EqualTo(2));
            Assert.That(session.PendingSpin, Is.Null);
            Assert.That(session.TryCompleteResolution(ticket), Is.False);
            Assert.That(session.ZoneNumber, Is.EqualTo(2));
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
        }

        [Test]
        public void ResolveAndContinue_DoNotConsumeAnotherLandingSample()
        {
            SequenceRandomSource landingRandom = new SequenceRandomSource(0);
            RunSession session = new RunSession(
                new FakeSettingsSource(), new SequenceRandomSource(0), landingRandom);

            SpinTicket first = EarnReward(session);
            Assert.That(session.TryResolveSpin(first), Is.False);
            Assert.That(session.TryCompleteResolution(first), Is.False);
            Assert.That(landingRandom.SamplesConsumed, Is.EqualTo(1));

            EarnReward(session);
            Assert.That(landingRandom.SamplesConsumed, Is.EqualTo(2));
        }

        [Test]
        public void NullCompletionTickets_AreRejectedWithoutChangingTheAcceptedSpin()
        {
            RunSession session = CreateSession(new FakeSettingsSource());
            Assert.That(session.TryResolveSpin(null), Is.False);
            Assert.That(session.TryCompleteResolution(null), Is.False);
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.TryResolveSpin(null), Is.False);
            Assert.That(session.PendingSpin, Is.SameAs(ticket));
            Assert.That(session.State, Is.EqualTo(RunState.Spinning));

            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.TryCompleteResolution(null), Is.False);
            Assert.That(session.PendingSpin, Is.SameAs(ticket));
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
        }

        [Test]
        public void ForeignTickets_AreRejectedEvenWhenTheirPublicIdentityValuesMatch()
        {
            RunSession first = CreateSession(new FakeSettingsSource());
            RunSession second = CreateSession(new FakeSettingsSource());
            Assert.That(first.TryStartSpin(out SpinTicket firstTicket), Is.True);
            Assert.That(second.TryStartSpin(out SpinTicket secondTicket), Is.True);
            Assert.That(firstTicket.RunGeneration, Is.EqualTo(secondTicket.RunGeneration));
            Assert.That(firstTicket.SpinSequence, Is.EqualTo(secondTicket.SpinSequence));
            Assert.That(firstTicket.ZoneNumber, Is.EqualTo(secondTicket.ZoneNumber));
            Assert.That(firstTicket.SelectedSlotIndex, Is.EqualTo(secondTicket.SelectedSlotIndex));

            Assert.That(first.TryResolveSpin(secondTicket), Is.False);
            Assert.That(first.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(first.TryResolveSpin(firstTicket), Is.True);
            Assert.That(first.TryCompleteResolution(secondTicket), Is.False);
            Assert.That(first.State, Is.EqualTo(RunState.Resolving));
            Assert.That(first.TryCompleteResolution(firstTicket), Is.True);
        }

        [Test]
        public void PreviousSpinTicket_CannotSettleOrAdvanceTheNextSpin()
        {
            RunSession session = CreateSession(new FakeSettingsSource());
            SpinTicket first = EarnReward(session);
            Assert.That(session.TryStartSpin(out SpinTicket second), Is.True);
            Assert.That(second.SpinSequence, Is.EqualTo(first.SpinSequence + 1));
            Assert.That(session.TryResolveSpin(first), Is.False);
            Assert.That(session.PendingSpin, Is.SameAs(second));
            Assert.That(session.TryResolveSpin(second), Is.True);
            Assert.That(session.TryCompleteResolution(first), Is.False);
            Assert.That(session.ZoneNumber, Is.EqualTo(2));
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 20));
            Assert.That(session.TryCompleteResolution(second), Is.True);
        }

        [Test]
        public void Restart_InvalidatesAnOldRunTicketAndAlwaysStartsAtZoneOne()
        {
            RunSession session = new RunSession(
                new FakeSettingsSource(), new SequenceRandomSource(0), new SequenceRandomSource(0), 5);
            Assert.That(session.TryStartSpin(out SpinTicket oldTicket), Is.True);
            long oldGeneration = session.RunGeneration;

            session.Restart();
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.RunGeneration, Is.EqualTo(oldGeneration + 1));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(session.TryStartSpin(out SpinTicket newTicket), Is.True);
            Assert.That(newTicket.SpinSequence, Is.EqualTo(1));
            Assert.That(session.TryResolveSpin(oldTicket), Is.False);
            Assert.That(session.TryResolveSpin(newTicket), Is.True);
            Assert.That(session.TryCompleteResolution(oldTicket), Is.False);
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
        }

        [Test]
        public void Bomb_ClearsTheLedgerOnceAndFailsAfterResolutionCompletes()
        {
            RunSession session = CreateSession(new FakeSettingsSource(), 0, 0.999);
            EarnReward(session);
            RunSnapshot earned = session.CreateSnapshot();
            Assert.That(session.TryStartSpin(out SpinTicket bomb), Is.True);
            Assert.That(bomb.Outcome.OutcomeKind, Is.EqualTo(SlotOutcomeKind.Bomb));

            Assert.That(session.TryResolveSpin(bomb), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Resolving));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
            AssertRewards(earned.Rewards, (RewardId.Cash, 10));
            Assert.That(session.TryResolveSpin(bomb), Is.False);
            Assert.That(session.TryCashOut(out _), Is.False);
            Assert.That(session.TryCompleteResolution(bomb), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Failed));
            Assert.That(session.Summary, Is.Null);
            Assert.That(session.CanSpin, Is.False);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryStartSpin(out _), Is.False);
            Assert.That(session.TryCompleteResolution(bomb), Is.False);

            session.Restart();
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
        }

        [Test]
        public void EmptyCashOut_IsAllowedOnceAndProducesAnEmptyTerminalSummary()
        {
            RunSession session = CreateSession(new FakeSettingsSource());
            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);
            Assert.That(summary.Rewards, Is.Empty);
            Assert.That(summary.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(session.Summary, Is.SameAs(summary));
            Assert.That(session.CanSpin, Is.False);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryStartSpin(out _), Is.False);
            Assert.That(session.TryCashOut(out RunSummary duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
            Assert.That(session.Summary, Is.SameAs(summary));
        }

        [Test]
        public void CashOut_AggregatesByIdentityInFirstEarnedOrderAndConsumesTheLedger()
        {
            FakeSettingsSource source = new FakeSettingsSource();
            RunSession session = CreateSession(source);
            source.SetReward(RewardId.Gold, 7);
            EarnReward(session);
            source.SetReward(RewardId.Cash, 5);
            EarnReward(session);
            EarnReward(session);

            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);
            AssertRewards(summary.Rewards, (RewardId.Cash, 15), (RewardId.Gold, 7));
            Assert.That(summary.ZoneNumber, Is.EqualTo(4));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
        }

        [TestCase(long.MaxValue - 3)]
        [TestCase(long.MaxValue)]
        public void AggregatedQuantities_SaturateAtLongMaxValue(long firstQuantity)
        {
            FakeSettingsSource source = new FakeSettingsSource();
            source.SetReward(RewardId.Cash, firstQuantity);
            RunSession session = CreateSession(source);
            source.SetReward(RewardId.Cash, 10);
            EarnReward(session);
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, firstQuantity));
            EarnReward(session);
            EarnReward(session);

            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);
            AssertRewards(summary.Rewards, (RewardId.Cash, long.MaxValue));
        }

        [Test]
        public void SnapshotsAndSummary_RemainDetachedAndCannotMutateTheLedgerThroughACast()
        {
            RunSession session = CreateSession(new FakeSettingsSource());
            EarnReward(session);
            RunSnapshot first = session.CreateSnapshot();
            AssertCannotMutate(first.Rewards);
            EarnReward(session);
            AssertRewards(first.Rewards, (RewardId.Cash, 10));
            Assert.That(first.ZoneNumber, Is.EqualTo(2));
            Assert.That(first.State, Is.EqualTo(RunState.Ready));

            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);
            AssertCannotMutate(summary.Rewards);
            RunSnapshot collected = session.CreateSnapshot();
            long collectedGeneration = session.RunGeneration;
            session.Restart();

            AssertRewards(summary.Rewards, (RewardId.Cash, 20));
            Assert.That(summary.RunGeneration, Is.EqualTo(collectedGeneration));
            Assert.That(collected.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(collected.Summary, Is.SameAs(summary));
            Assert.That(session.Summary, Is.Null);
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(session.TryCashOut(out RunSummary nextSummary), Is.True);
            Assert.That(nextSummary.Rewards, Is.Empty);
        }

        [TestCase(4, true)]
        [TestCase(5, false)]
        [TestCase(29, true)]
        [TestCase(30, false)]
        public void RestrictedCashOut_UsesTheDisplayedUpcomingZone(int completedZone, bool allowed)
        {
            FakeSettingsSource source = new FakeSettingsSource();
            source.SetRules(5, 30, CashOutPolicy.SafeAndSuperZonesOnly);
            RunSession session = new RunSession(
                source, new SequenceRandomSource(0), new SequenceRandomSource(0), completedZone);
            EarnReward(session);

            Assert.That(session.ZoneNumber, Is.EqualTo(completedZone + 1));
            Assert.That(session.CanCashOut, Is.EqualTo(allowed));
            Assert.That(session.CreateSnapshot().CanCashOut, Is.EqualTo(allowed));
            Assert.That(session.TryCashOut(out RunSummary summary), Is.EqualTo(allowed));
            if (allowed)
            {
                AssertRewards(summary.Rewards, (RewardId.Cash, 10));
            }
            else
            {
                Assert.That(summary, Is.Null);
                Assert.That(session.State, Is.EqualTo(RunState.Ready));
                AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
            }
        }

        [Test]
        public void PolicyEdits_ChangeReadyEligibilityImmediatelyAndAreRecheckedByCashOut()
        {
            FakeSettingsSource source = new FakeSettingsSource();
            RunSession session = CreateSession(source);
            RunSnapshot beforeEdit = session.CreateSnapshot();
            source.SetRules(5, 30, CashOutPolicy.SafeAndSuperZonesOnly);

            Assert.That(beforeEdit.CanCashOut, Is.True);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryCashOut(out _), Is.False);
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            source.SetRules(5, 30, CashOutPolicy.AnyZone);
            Assert.That(session.CanCashOut, Is.True);
            Assert.That(session.TryCashOut(out _), Is.True);
            Assert.That(source.WheelCreationCount, Is.EqualTo(1));
        }

        [TestCase(1, 1, ZoneType.Normal, ZoneType.Safe)]
        [TestCase(5, 100, ZoneType.Safe, ZoneType.Normal)]
        [TestCase(30, 100, ZoneType.Super, ZoneType.Normal)]
        public void IntervalEdits_PreserveTheDisplayedTypeUntilTheNextWheelIsPrepared(
            int initialZone, int newInterval, ZoneType preparedType, ZoneType nextType)
        {
            FakeSettingsSource source = new FakeSettingsSource();
            source.SetRules(5, 30, CashOutPolicy.SafeAndSuperZonesOnly);
            RunSession session = new RunSession(
                source, new SequenceRandomSource(0), new SequenceRandomSource(0), initialZone);
            source.SetRules(newInterval, 100, CashOutPolicy.SafeAndSuperZonesOnly);

            Assert.That(session.Wheel.ZoneType, Is.EqualTo(preparedType));
            Assert.That(session.CanCashOut, Is.EqualTo(preparedType != ZoneType.Normal));
            EarnReward(session);
            Assert.That(session.Wheel.ZoneType, Is.EqualTo(nextType));
            Assert.That(session.CanCashOut, Is.EqualTo(nextType != ZoneType.Normal));
        }

        [Test]
        public void MidSpinEdits_AffectOnlyTheNextPreparedWheelAndNeverTheAcceptedOutcome()
        {
            FakeSettingsSource source = new FakeSettingsSource();
            RunSession session = CreateSession(source);
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            source.SetReward(RewardId.Gold, 250);
            source.SetLandingWeight(7);
            source.SetRules(1, 30, CashOutPolicy.SafeAndSuperZonesOnly);

            Assert.That(ticket.Wheel, Is.SameAs(session.Wheel));
            Assert.That(ticket.Outcome.RewardId, Is.EqualTo(RewardId.Cash));
            Assert.That(ticket.Outcome.Quantity, Is.EqualTo(10));
            Assert.That(ticket.Outcome.LandingWeight, Is.EqualTo(1));
            Assert.That(ticket.Wheel.ZoneType, Is.EqualTo(ZoneType.Normal));
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
            Assert.That(session.TryCompleteResolution(ticket), Is.True);

            Assert.That(session.Wheel.ZoneType, Is.EqualTo(ZoneType.Safe));
            Assert.That(session.Wheel.Slots[0].RewardId, Is.EqualTo(RewardId.Gold));
            Assert.That(session.Wheel.Slots[0].Quantity, Is.EqualTo(250));
            Assert.That(session.Wheel.Slots[0].LandingWeight, Is.EqualTo(7));
            Assert.That(session.Wheel, Is.Not.SameAs(ticket.Wheel));
            Assert.That(source.WheelCreationCount, Is.EqualTo(2));
        }

        [Test]
        public void FailedNextWheelPreparation_PreservesTheSettledRewardAndCanBeRetried()
        {
            FakeSettingsSource source = new FakeSettingsSource();
            RunSession session = CreateSession(source);
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            source.FailPreparationAt(2);

            Assert.Throws<InvalidOperationException>(() => session.TryCompleteResolution(ticket));
            Assert.That(session.State, Is.EqualTo(RunState.Resolving));
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.PendingSpin, Is.SameAs(ticket));
            Assert.That(session.Wheel, Is.SameAs(ticket.Wheel));
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
            Assert.That(session.TryResolveSpin(ticket), Is.False);

            source.ClearPreparationFailure();
            Assert.That(session.TryCompleteResolution(ticket), Is.True);
            Assert.That(session.ZoneNumber, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            AssertRewards(session.CreateSnapshot().Rewards, (RewardId.Cash, 10));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedRestartPreparation_PreservesThePriorRunUntilRetrySucceeds(bool cashOutFirst)
        {
            FakeSettingsSource source = new FakeSettingsSource();
            RunSession session = CreateSession(source);
            EarnReward(session);
            if (cashOutFirst)
            {
                Assert.That(session.TryCashOut(out _), Is.True);
            }
            RunSnapshot before = session.CreateSnapshot();
            source.FailPreparationAt(1);

            Assert.Throws<InvalidOperationException>(() => session.Restart());
            Assert.That(session.RunGeneration, Is.EqualTo(before.RunGeneration));
            Assert.That(session.State, Is.EqualTo(before.State));
            Assert.That(session.Wheel, Is.SameAs(before.Wheel));
            Assert.That(session.Summary, Is.SameAs(before.Summary));
            Assert.That(session.CreateSnapshot().Rewards.Select(reward => reward.Quantity),
                Is.EqualTo(before.Rewards.Select(reward => reward.Quantity)));

            source.ClearPreparationFailure();
            session.Restart();
            Assert.That(session.RunGeneration, Is.EqualTo(before.RunGeneration + 1));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.Summary, Is.Null);
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
        }

        [Test]
        public void InvalidLandingSample_DoesNotAcceptASpinAndCanBeRetried()
        {
            SequenceRandomSource landingRandom = new SequenceRandomSource(-0.1, 0);
            RunSession session = new RunSession(
                new FakeSettingsSource(), new SequenceRandomSource(0), landingRandom);

            Assert.Throws<InvalidOperationException>(() => session.TryStartSpin(out _));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
            Assert.That(session.PendingSpin, Is.Null);
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(session.TryStartSpin(out SpinTicket accepted), Is.True);
            Assert.That(accepted.SpinSequence, Is.EqualTo(1));
            Assert.That(landingRandom.SamplesConsumed, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidInitialZone_IsRejectedBeforeAnyWheelGeneration(int zoneNumber)
        {
            FakeSettingsSource source = new FakeSettingsSource();
            SequenceRandomSource generationRandom = new SequenceRandomSource(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunSession(
                source, generationRandom, new SequenceRandomSource(0), zoneNumber));
            Assert.That(source.WheelCreationCount, Is.Zero);
            Assert.That(generationRandom.SamplesConsumed, Is.Zero);
        }

        [Test]
        public void MaximumZoneReward_CompletesIntoCashOutWithoutOverflowOrAnotherWheel()
        {
            FakeSettingsSource source = new FakeSettingsSource();
            source.SetRules(5, 30, CashOutPolicy.SafeAndSuperZonesOnly);
            RunSession session = new RunSession(
                source, new SequenceRandomSource(0), new SequenceRandomSource(0), int.MaxValue);
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.TryCompleteResolution(ticket), Is.True);

            Assert.That(session.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(session.ZoneNumber, Is.EqualTo(int.MaxValue));
            Assert.That(session.Summary.ZoneNumber, Is.EqualTo(int.MaxValue));
            AssertRewards(session.Summary.Rewards, (RewardId.Cash, 10));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(session.PendingSpin, Is.Null);
            Assert.That(source.WheelCreationCount, Is.EqualTo(1));
            Assert.That(session.TryCompleteResolution(ticket), Is.False);
            Assert.That(session.TryCashOut(out _), Is.False);

            session.Restart();
            Assert.That(session.ZoneNumber, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(RunState.Ready));
        }

        [Test]
        public void MaximumZoneBomb_FailsWithoutProducingACashOutSummary()
        {
            RunSession session = new RunSession(
                new FakeSettingsSource(), new SequenceRandomSource(0), new SequenceRandomSource(0.999), int.MaxValue);
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.TryCompleteResolution(ticket), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Failed));
            Assert.That(session.ZoneNumber, Is.EqualTo(int.MaxValue));
            Assert.That(session.Summary, Is.Null);
        }

        private static RunSession CreateSession(FakeSettingsSource source, params double[] landingSamples)
        {
            return new RunSession(source, new SequenceRandomSource(0),
                new SequenceRandomSource(landingSamples.Length == 0 ? new[] { 0.0 } : landingSamples));
        }

        private static SpinTicket EarnReward(RunSession session)
        {
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(ticket.Outcome.OutcomeKind, Is.EqualTo(SlotOutcomeKind.Reward));
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.TryCompleteResolution(ticket), Is.True);
            return ticket;
        }

        private static void AssertRewards(IReadOnlyList<RewardAmount> rewards, params (RewardId, long)[] expected)
        {
            Assert.That(rewards.Select(reward => (reward.RewardId, reward.Quantity)), Is.EqualTo(expected));
        }

        private static void AssertCannotMutate(IReadOnlyList<RewardAmount> rewards)
        {
            if (rewards is IList<RewardAmount> mutableView)
            {
                Assert.Throws<NotSupportedException>(() => mutableView.Add(new RewardAmount(RewardId.Gold, 1)));
                Assert.Throws<NotSupportedException>(() => mutableView.Clear());
                if (mutableView.Count > 0)
                {
                    Assert.Throws<NotSupportedException>(() => mutableView[0] = new RewardAmount(RewardId.Gold, 1));
                }
            }
        }

        private sealed class FakeSettingsSource : IRunSettingsSource
        {
            private ZoneRules _rules = new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, long.MaxValue);
            private RewardId _rewardId = RewardId.Cash;
            private long _quantity = 10;
            private int _landingWeight = 1;
            private int _failureZone;
            private int _wheelCreationCount;

            internal int WheelCreationCount => _wheelCreationCount;

            public ZoneRules CreateZoneRules()
            {
                return _rules;
            }

            public WheelData CreateWheelData(int zoneNumber, IRandomSource random)
            {
                _wheelCreationCount++;
                if (zoneNumber == _failureZone)
                {
                    throw new InvalidOperationException("The authored wheel is temporarily invalid.");
                }

                random.NextDouble();
                ZoneType type = _rules.GetZoneType(zoneNumber);
                WheelSlotData[] slots = new WheelSlotData[WheelData.SlotCount];
                slots[0] = WheelSlotData.CreateReward(_rewardId, _quantity, _landingWeight);
                for (int index = 1; index < slots.Length; index++)
                {
                    slots[index] = index == slots.Length - 1 && type == ZoneType.Normal
                        ? WheelSlotData.CreateBomb()
                        : WheelSlotData.CreateReward(RewardId.Cash, 1000 + index);
                }
                return new WheelData(zoneNumber, type, slots);
            }

            internal void SetReward(RewardId rewardId, long quantity)
            {
                _rewardId = rewardId;
                _quantity = quantity;
            }

            internal void SetLandingWeight(int landingWeight)
            {
                _landingWeight = landingWeight;
            }

            internal void SetRules(int safeInterval, int superInterval, CashOutPolicy policy)
            {
                _rules = new ZoneRules(safeInterval, superInterval, policy, 5, long.MaxValue);
            }

            internal void FailPreparationAt(int zoneNumber)
            {
                _failureZone = zoneNumber;
            }

            internal void ClearPreparationFailure()
            {
                _failureZone = 0;
            }
        }
    }
}
