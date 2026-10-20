using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VertigoWheel.App;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Dialogs;
using VertigoWheel.UI.Rewards;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    /// <summary>Exercises authored scene controls and overlays against an injected deterministic session.</summary>
    public sealed class GameFlowTests
    {
        private readonly List<Object> _clones = new List<Object>();
        private Scene _scene;
        private GameController _controller;
        private GameSettings _settings;
        private RunSession _run;
        private CountingRandomSource _wheelRandom;
        private MutableRandomSource _landingRandom;
        private GameView _view;
        private RewardRevealView _reward;
        private ExitConfirmationView _exit;
        private BombResultView _bomb;
        private CashOutSummaryView _summary;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AsyncOperation loading = SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Game.unity", LoadSceneMode.Additive);
            Assert.That(loading, Is.Not.Null, "The authored Game scene must be in Build Settings.");
            yield return loading;
            _scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            GameBootstrap bootstrap = FindInScene<GameBootstrap>();
            Assert.That(bootstrap.Controller, Is.Not.Null, "The authored scene must compose a playable controller.");
            GameSettings authored = ReadField<GameSettings>(bootstrap, "_settings");
            bootstrap.enabled = false;

            _settings = Clone(authored);
            _settings.ZoneSettings = Clone(authored.ZoneSettings);
            _settings.AnimationSettings = Clone(authored.AnimationSettings);
            _settings.BronzeWheel = Clone(authored.BronzeWheel);
            _settings.SilverWheel = Clone(authored.SilverWheel);
            _settings.GoldenWheel = Clone(authored.GoldenWheel);
            _settings.ZoneSettings.SafeZoneInterval = 5;
            _settings.ZoneSettings.SuperZoneInterval = 30;
            _settings.ZoneSettings.CashOutPolicy = CashOutPolicy.AnyZone;
            _settings.AnimationSettings.SpinDurationSeconds = 2f;
            _settings.AnimationSettings.FullTurns = 1;
            _settings.AnimationSettings.DialogDurationSeconds = 0f;
            _settings.AnimationSettings.RewardRevealDurationSeconds = 0f;

            _view = FindInScene<GameView>();
            _reward = FindInScene<RewardRevealView>();
            _exit = FindInScene<ExitConfirmationView>();
            _bomb = FindInScene<BombResultView>();
            _summary = FindInScene<CashOutSummaryView>();
            _wheelRandom = new CountingRandomSource(321);
            _landingRandom = new MutableRandomSource();
            _run = new RunSession(_settings, _wheelRandom, _landingRandom);
            _controller = new GameController(_settings, _run, _view, _reward, _exit, _bomb, _summary);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _controller?.Dispose();
            _controller = null;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            foreach (Object clone in _clones)
            {
                Object.Destroy(clone);
            }
            _clones.Clear();
            _settings = null;
            _run = null;
            _view = null;
            _reward = null;
            _exit = null;
            _bomb = null;
            _summary = null;
            yield return null;
        }

        [Test]
        public void ExitConfirmation_CancelPreservesPreparedWheelAndRandomStream_AndBlocksBoard()
        {
            WheelData prepared = _run.Wheel;
            int randomCalls = _wheelRandom.CallCount;

            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            ClickSpin();
            ClickBoardExit();
            Assert.That(_run.PendingSpin, Is.Null);
            ClickAction(_exit, "Go Back");

            Assert.That(_exit.gameObject.activeSelf, Is.False);
            Assert.That(_run.Wheel, Is.SameAs(prepared));
            Assert.That(_view.Wheel.BoundWheel, Is.SameAs(prepared));
            Assert.That(_run.ZoneNumber, Is.EqualTo(1));
            Assert.That(_wheelRandom.CallCount, Is.EqualTo(randomCalls));
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
        }

        [Test]
        public void Reward_ResumeSettlesOnce_AndContinueAdvancesOneZoneWithoutSpinning()
        {
            SpinTicket ticket = StartRewardSpin();
            ClickSpin();
            ClickBoardExit();
            Assert.That(_run.PendingSpin, Is.SameAs(ticket));
            _controller.Resume();
            _controller.Resume();

            Assert.That(_run.State, Is.EqualTo(RunState.Resolving));
            Assert.That(_reward.IsReady, Is.True);
            AssertRewardSettledOnce(ticket);
            ClickAction(_reward);
            ClickAction(_reward);

            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
            Assert.That(_run.PendingSpin, Is.Null);
            Assert.That(_view.Wheel.BoundWheel, Is.SameAs(_run.Wheel));
            AssertRewardSettledOnce(ticket);
        }

        [UnityTest]
        public IEnumerator RewardEntrance_BlocksContinueUntilReadable()
        {
            _settings.AnimationSettings.RewardRevealDurationSeconds = 0.1f;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            Assert.That(_reward.IsReady, Is.False);
            ClickAction(_reward);
            Assert.That(_run.State, Is.EqualTo(RunState.Resolving));
            Assert.That(_run.ZoneNumber, Is.EqualTo(1));
            AssertRewardSettledOnce(ticket);

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_reward.IsReady, Is.True);
            ClickAction(_reward);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
        }

        [Test]
        public void CashOut_ConsumesLedgerOnce_ShowsDetachedSummary_AndRestartStartsEmptyRun()
        {
            SpinTicket ticket = SettleRewardAndContinue();
            long generation = _run.RunGeneration;
            ClickBoardExit();
            ClickAction(_exit, "Collect Rewards");
            RunSummary collected = _run.Summary;

            Assert.That(_run.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(_summary.IsReady, Is.True);
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(collected.Rewards.Count, Is.EqualTo(1));
            Assert.That(collected.Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
            ClickAction(_exit, "Collect Rewards");
            Assert.That(_run.Summary, Is.SameAs(collected));

            ClickAction(_summary);
            ClickAction(_summary);
            Assert.That(_run.RunGeneration, Is.EqualTo(generation + 1));
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(1));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(_run.Summary, Is.Null);
            Assert.That(collected.Rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
        }

        [Test]
        public void ExitBeforeAnySpin_ShowsAnEmptyCollectedSummary()
        {
            int randomCalls = _wheelRandom.CallCount;
            ClickBoardExit();
            ClickAction(_exit, "Collect Rewards");

            Assert.That(_run.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(_run.Summary.Rewards, Is.Empty);
            Assert.That(_summary.IsReady, Is.True);
            Assert.That(_wheelRandom.CallCount, Is.EqualTo(randomCalls));
        }

        [Test]
        public void Bomb_ClearsUnclaimedRewards_ShowsFailure_AndRestartResetsTheRun()
        {
            SettleRewardAndContinue();
            Assert.That(_run.CreateSnapshot().Rewards, Is.Not.Empty);
            RewardAmount earned = _run.CreateSnapshot().Rewards[0];
            _landingRandom.Sample = SampleFor(_run.Wheel, SlotOutcomeKind.Bomb);
            ClickSpin();
            _controller.Resume();

            Assert.That(_run.State, Is.EqualTo(RunState.Failed));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(_bomb.IsReady, Is.True);
            RewardCollectionView lost = _bomb.GetComponentInChildren<RewardCollectionView>();
            Assert.That(lost.GetComponent<UnityEngine.UI.ScrollRect>().enabled, Is.True);
            RewardItemView row = lost.GetComponentInChildren<RewardItemView>();
            Assert.That(row.GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo(earned.Quantity.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(_bomb.GetComponentsInChildren<ActionButtonView>().Any(b => b.name == "ui_button_exit"), Is.False);
            Assert.That(GetAction(_view).IsInteractable, Is.False);
            long generation = _run.RunGeneration;
            ClickAction(_bomb);
            ClickAction(_bomb);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(1));
            Assert.That(_run.RunGeneration, Is.EqualTo(generation + 1));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
        }

        [Test]
        public void FailedBombRestart_RetainsLostRewardDisplayUntilRestartSucceeds()
        {
            SettleRewardAndContinue();
            _landingRandom.Sample = SampleFor(_run.Wheel, SlotOutcomeKind.Bomb);
            ClickSpin();
            _controller.Resume();
            var list = _bomb.GetComponentInChildren<RewardCollectionView>();
            string quantity = list.GetComponentInChildren<TMP_Text>().text;
            RewardPoolSettings pool = _settings.BronzeWheel.Pool;
            _settings.BronzeWheel.Pool = null;
            ClickAction(_bomb);
            Assert.That(_run.State, Is.EqualTo(RunState.Failed));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(_bomb.IsReady, Is.True);
            Assert.That(list.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(quantity));
            _settings.BronzeWheel.Pool = pool;
            ClickAction(_bomb);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_bomb.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PolicyEditWhileConfirmationIsOpen_IsRecheckedOnCollect_WithoutRerolling()
        {
            WheelData prepared = _run.Wheel;
            ClickBoardExit();
            _settings.ZoneSettings.CashOutPolicy = CashOutPolicy.SafeAndSuperZonesOnly;
            ClickAction(_exit, "Collect Rewards");

            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.Summary, Is.Null);
            Assert.That(_run.Wheel, Is.SameAs(prepared));
            Assert.That(GetAction(_view).IsInteractable, Is.False);

            _settings.ZoneSettings.CashOutPolicy = CashOutPolicy.AnyZone;
            _controller.RefreshReadyState();
            Assert.That(GetAction(_view).IsInteractable, Is.True);
            Assert.That(_run.Wheel, Is.SameAs(prepared));
            Assert.That(_view.Wheel.BoundWheel, Is.SameAs(prepared));
        }

        [Test]
        public void InvalidNextPoolEdit_LeavesSettledRewardRetryable_WithoutGrantingItAgain()
        {
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            RewardPoolSettings original = _settings.BronzeWheel.Pool;
            _settings.BronzeWheel.Pool = null;
            ClickAction(_reward);

            Assert.That(_run.State, Is.EqualTo(RunState.Resolving));
            Assert.That(_run.PendingSpin, Is.SameAs(ticket));
            Assert.That(_run.ZoneNumber, Is.EqualTo(1));
            Assert.That(_reward.IsReady, Is.True, "Fixing a developer edit must allow Continue to retry.");
            AssertRewardSettledOnce(ticket);

            _settings.BronzeWheel.Pool = original;
            ClickAction(_reward);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
            AssertRewardSettledOnce(ticket);
        }

        [Test]
        public void InvalidMidSpinEdits_DoNotPreventAcceptedOutcomeOrReveal_AndCanBeCorrected()
        {
            SpinTicket ticket = StartRewardSpin();
            // Inspector serialization bypasses setters, so simulate that exact path.
            WriteField(_settings.AnimationSettings, "_spinDurationSeconds", -1f);
            WriteField(_settings.ZoneSettings, "_safeZoneInterval", 0);
            _controller.Resume();

            Assert.That(_run.State, Is.EqualTo(RunState.Resolving));
            Assert.That(_reward.IsReady, Is.True, "The accepted action keeps its valid animation snapshot.");
            AssertRewardSettledOnce(ticket);
            ClickAction(_reward);
            Assert.That(_run.State, Is.EqualTo(RunState.Resolving));
            Assert.That(_reward.IsReady, Is.True);

            _settings.AnimationSettings.SpinDurationSeconds = 2f;
            _settings.ZoneSettings.SafeZoneInterval = 5;
            ClickAction(_reward);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
            AssertRewardSettledOnce(ticket);
        }

        [Test]
        public void Disposal_CancelsOwnedAnimationAndListeners_WithoutSettlingTheAbandonedSpin()
        {
            SpinTicket ticket = StartRewardSpin();
            _controller.Dispose();
            _controller.Dispose();
            _controller.Resume();
            ClickSpin();
            ClickBoardExit();

            Assert.That(_view.WheelAnimator.IsSpinning, Is.False);
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
            Assert.That(_run.PendingSpin, Is.SameAs(ticket));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(_exit.gameObject.activeSelf, Is.False);
            Assert.That(_reward.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void IncompleteZoneStyleEdit_DoesNotInterruptSettlementOrContinue()
        {
            ZoneTrackView track = FindInScene<ZoneTrackView>();
            ZoneTrackSettings style = Clone(ReadField<ZoneTrackSettings>(track, "_settings"));
            WriteField(track, "_settings", style);
            SpinTicket ticket = StartRewardSpin();
            style.BackgroundSprite = null;
            style.CurrentBackgroundSprite = null;

            _controller.Resume();
            Assert.That(_reward.IsReady, Is.True);
            AssertRewardSettledOnce(ticket);
            ClickAction(_reward);

            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
            AssertRewardSettledOnce(ticket);
            Assert.That(track.GetComponentsInChildren<TMP_Text>().Single(t => t.fontStyle == FontStyles.Bold).text,
                Is.EqualTo("2"));
        }

        [Test]
        public void AcceptedRewardAndBomb_KeepTheirVisualsWhenAuthoredIconsAreClearedMidSpin()
        {
            RewardId selectedId = RewardId.None;
            foreach (WheelSlotData slot in _run.Wheel.Slots)
            {
                if (slot.OutcomeKind == SlotOutcomeKind.Reward)
                {
                    selectedId = slot.RewardId;
                    break;
                }
            }
            // Isolate this visual asset so the regression never edits a project asset.
            Dictionary<RewardId, RewardSettings> catalog =
                ReadField<Dictionary<RewardId, RewardSettings>>(_controller, "_sourceRewards");
            RewardSettings reward = Clone(catalog[selectedId]);
            catalog[selectedId] = reward;
            Sprite rewardIcon = reward.Icon;
            Sprite bombIcon = _settings.BombIcon;

            reward.Icon = null;
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Ready), "Invalid metadata must reject Spin before accepting a ticket.");
            Assert.That(_run.PendingSpin, Is.Null);
            reward.Icon = rewardIcon;

            SpinTicket ticket = StartRewardSpin();
            Assert.That(ticket.Outcome.RewardId, Is.EqualTo(selectedId));
            reward.Icon = null;
            _settings.BombIcon = null;
            _controller.Resume();
            Assert.That(_reward.IsReady, Is.True);
            Assert.That(ReadField<UnityEngine.UI.Image>(_reward, "_icon").sprite, Is.EqualTo(rewardIcon));
            AssertRewardSettledOnce(ticket);

            reward.Icon = rewardIcon;
            _settings.BombIcon = bombIcon;
            ClickAction(_reward);
            _landingRandom.Sample = SampleFor(_run.Wheel, SlotOutcomeKind.Bomb);
            ClickSpin();
            _settings.BombIcon = null;
            _controller.Resume();
            Assert.That(_run.State, Is.EqualTo(RunState.Failed));
            Assert.That(_bomb.IsReady, Is.True);
            Assert.That(ReadField<UnityEngine.UI.Image>(_bomb, "_icon").sprite, Is.EqualTo(bombIcon));
        }

        private SpinTicket StartRewardSpin()
        {
            _landingRandom.Sample = SampleFor(_run.Wheel, SlotOutcomeKind.Reward);
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
            return _run.PendingSpin;
        }

        private SpinTicket SettleRewardAndContinue()
        {
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            Assert.That(_reward.IsReady, Is.True);
            ClickAction(_reward);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            return ticket;
        }

        private void AssertRewardSettledOnce(SpinTicket ticket)
        {
            var rewards = _run.CreateSnapshot().Rewards;
            Assert.That(rewards.Count, Is.EqualTo(1));
            Assert.That(rewards[0].RewardId, Is.EqualTo(ticket.Outcome.RewardId));
            Assert.That(rewards[0].Quantity, Is.EqualTo(ticket.Outcome.Quantity));
        }

        private void ClickSpin()
        {
            Transform button = _view.Wheel.transform.Find("ui_button_spin");
            Assert.That(button, Is.Not.Null);
            button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        private void ClickBoardExit() => ClickAction(_view);

        private static void ClickAction(Component owner, string label = null)
        {
            GetAction(owner, label).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        private static ActionButtonView GetAction(Component owner, string label = null)
        {
            ActionButtonView[] actions = owner.GetComponentsInChildren<ActionButtonView>(true);
            foreach (ActionButtonView action in actions)
            {
                TMP_Text text = action.GetComponentInChildren<TMP_Text>(true);
                if (label == null || (text != null && string.Equals(text.text.Trim(), label, StringComparison.OrdinalIgnoreCase)))
                {
                    return action;
                }
            }
            Assert.Fail(owner.name + " is missing its action " + label);
            return null;
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
            Assert.Fail("The authored scene is missing " + typeof(T).Name);
            return null;
        }

        private T Clone<T>(T source) where T : Object
        {
            T clone = Object.Instantiate(source);
            _clones.Add(clone);
            return clone;
        }

        private static T ReadField<T>(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
            return (T)info.GetValue(target);
        }

        private static void WriteField<T>(object target, string field, T value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
            info.SetValue(target, value);
        }

        private static double SampleFor(WheelData wheel, SlotOutcomeKind kind)
        {
            long total = 0;
            foreach (WheelSlotData slot in wheel.Slots)
            {
                total += slot.LandingWeight;
            }
            long preceding = 0;
            foreach (WheelSlotData slot in wheel.Slots)
            {
                if (slot.OutcomeKind == kind)
                {
                    return (preceding + slot.LandingWeight / 2.0) / total;
                }
                preceding += slot.LandingWeight;
            }
            throw new InvalidOperationException("The prepared wheel has no " + kind + " slot.");
        }

        private sealed class CountingRandomSource : IRandomSource
        {
            private readonly SystemRandomSource _source;
            private int _callCount;

            internal CountingRandomSource(int seed) => _source = new SystemRandomSource(seed);
            internal int CallCount => _callCount;

            public double NextDouble()
            {
                _callCount++;
                return _source.NextDouble();
            }
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
