using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class RunSettingsIntegrationTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void AuthoredSettings_CanPrepareResolveAndCashOutARealPoolReward()
        {
            GameSettings settings = CreateSettings();
            MutableRandomSource landing = new MutableRandomSource();
            RunSession session = new RunSession(settings, new SystemRandomSource(123), landing);
            landing.Sample = SampleForReward(session.Wheel);

            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.TryCompleteResolution(ticket), Is.True);
            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);

            Assert.That(summary.ZoneNumber, Is.EqualTo(2));
            Assert.That(summary.Rewards.Count, Is.EqualTo(1));
            Assert.That(summary.Rewards[0].RewardId, Is.EqualTo(ticket.Outcome.RewardId));
            Assert.That(summary.Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
            Assert.That(session.CreateSnapshot().Rewards, Is.Empty);
        }

        [Test]
        public void ScriptableObjectPolicyEdit_ChangesEligibilityWithoutRegeneratingPreparedWheel()
        {
            GameSettings settings = CreateSettings();
            RunSession session = new RunSession(settings, new SystemRandomSource(123), new SystemRandomSource(456));
            WheelData prepared = session.Wheel;

            Assert.That(session.CanCashOut, Is.True);
            settings.ZoneSettings.CashOutPolicy = CashOutPolicy.SafeAndSuperZonesOnly;
            Assert.That(session.CanCashOut, Is.False);
            Assert.That(session.TryCashOut(out _), Is.False);
            Assert.That(session.Wheel, Is.SameAs(prepared));

            settings.ZoneSettings.CashOutPolicy = CashOutPolicy.AnyZone;
            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);
            Assert.That(summary.Rewards, Is.Empty);
        }

        [Test]
        public void InvalidPoolEdit_DoesNotPreventBankingPreparedRewardsOrDestroyResultOnFailedRestart()
        {
            GameSettings settings = CreateSettings();
            WheelSettings bronze = Clone(settings.BronzeWheel);
            settings.BronzeWheel = bronze;
            MutableRandomSource landing = new MutableRandomSource();
            RunSession session = new RunSession(settings, new SystemRandomSource(123), landing);
            landing.Sample = SampleForReward(session.Wheel);
            Assert.That(session.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(session.TryResolveSpin(ticket), Is.True);
            Assert.That(session.TryCompleteResolution(ticket), Is.True);

            bronze.Pool = null;
            Assert.That(session.CanCashOut, Is.True);
            Assert.That(session.TryCashOut(out RunSummary summary), Is.True);
            Assert.That(summary.Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
            Assert.Throws<InvalidOperationException>(() => session.Restart());
            Assert.That(session.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(session.Summary, Is.SameAs(summary));
        }

        [Test]
        public void MultiplierEdit_KeepsTheAcceptedOutcomeAndBankedTotal_AndAffectsTheNextWheel()
        {
            GameSettings settings = CreateSettings();
            settings.BronzeWheel = Clone(settings.BronzeWheel);
            var landing = new MutableRandomSource();
            var run = new RunSession(settings, new SystemRandomSource(123), landing);
            WheelData prepared = run.Wheel;
            landing.Sample = SampleForReward(prepared);
            Assert.That(run.TryStartSpin(out SpinTicket ticket), Is.True);
            long accepted = ticket.Outcome.Quantity;
            settings.BronzeWheel.QuantityMultiplier = 10;
            Assert.That(run.Wheel, Is.SameAs(prepared));
            Assert.That(ticket.Outcome.Quantity, Is.EqualTo(accepted));
            Assert.That(run.TryResolveSpin(ticket), Is.True);
            Assert.That(run.TryCompleteResolution(ticket), Is.True);
            var nextEntries = settings.BronzeWheel.Pool.CreateEntries(2, settings.CreateZoneRules(), 10);
            Assert.That(run.Wheel.Slots.Where(s => s.OutcomeKind == SlotOutcomeKind.Reward)
                .All(s => nextEntries.Any(e => e.Reward.RewardId == s.RewardId && e.Reward.Quantity == s.Quantity)), Is.True);
            Assert.That(run.TryCashOut(out RunSummary collected), Is.True);
            Assert.That(collected.Rewards.Count, Is.EqualTo(1));
            Assert.That(collected.Rewards[0].Quantity, Is.EqualTo(accepted));
        }

        [Test]
        public void InvalidMultiplierEdit_KeepsAcknowledgmentRetryableWithoutGrantingAgain()
        {
            GameSettings settings = CreateSettings();
            settings.BronzeWheel = Clone(settings.BronzeWheel);
            var landing = new MutableRandomSource();
            var run = new RunSession(settings, new SystemRandomSource(123), landing);
            landing.Sample = SampleForReward(run.Wheel);
            Assert.That(run.TryStartSpin(out SpinTicket ticket), Is.True);
            Assert.That(run.TryResolveSpin(ticket), Is.True);
            using (var data = new SerializedObject(settings.BronzeWheel))
            {
                data.FindProperty("_quantityMultiplier").intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryCompleteResolution(ticket));
            Assert.That(run.State, Is.EqualTo(RunState.Resolving));
            Assert.That(run.PendingSpin, Is.SameAs(ticket));
            Assert.That(run.CreateSnapshot().Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
            settings.BronzeWheel.QuantityMultiplier = 2;
            Assert.That(run.TryCompleteResolution(ticket), Is.True);
            Assert.That(run.TryCompleteResolution(ticket), Is.False);
            Assert.That(run.TryResolveSpin(ticket), Is.False);
            Assert.That(run.CreateSnapshot().Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
        }

        private GameSettings CreateSettings()
        {
            GameSettings authored = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            Assert.That(authored, Is.Not.Null, "The committed GameSettings asset is required for this integration check.");
            GameSettings settings = Clone(authored);
            settings.ZoneSettings = Clone(authored.ZoneSettings);
            settings.ZoneSettings.SafeZoneInterval = 5;
            settings.ZoneSettings.SuperZoneInterval = 30;
            settings.ZoneSettings.CashOutPolicy = CashOutPolicy.AnyZone;
            return settings;
        }

        private T Clone<T>(T source) where T : UnityEngine.Object
        {
            T clone = UnityEngine.Object.Instantiate(source);
            _objects.Add(clone);
            return clone;
        }

        private static double SampleForReward(WheelData wheel)
        {
            long totalWeight = 0;
            foreach (WheelSlotData slot in wheel.Slots)
            {
                totalWeight += slot.LandingWeight;
            }
            long precedingWeight = 0;
            foreach (WheelSlotData slot in wheel.Slots)
            {
                if (slot.OutcomeKind == SlotOutcomeKind.Reward)
                {
                    return (precedingWeight + slot.LandingWeight / 2.0) / totalWeight;
                }
                precedingWeight += slot.LandingWeight;
            }
            throw new InvalidOperationException("The supplied wheel contains no rewards.");
        }

        private sealed class MutableRandomSource : IRandomSource
        {
            private double _sample;

            internal double Sample
            {
                get => _sample;
                set => _sample = value;
            }

            public double NextDouble() => _sample;
        }
    }
}
