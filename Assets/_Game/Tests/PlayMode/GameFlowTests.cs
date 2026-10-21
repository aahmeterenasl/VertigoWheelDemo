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
            _settings.AnimationSettings.SummaryEntranceSeconds = 0f;
            _settings.AnimationSettings.BombEntranceSeconds = 0f;
            _settings.AnimationSettings.ExitOpenSeconds = 0f;
            _settings.AnimationSettings.ExitCloseSeconds = 0f;
            _settings.AnimationSettings.ZoneTransitionSeconds = 0f;
            _settings.AnimationSettings.WheelCrossfadeSeconds = 0f;
            _settings.AnimationSettings.RewardRevealDurationSeconds = 0f;
            _settings.AnimationSettings.RewardFlightEnabled = false;

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
        public IEnumerator RewardArrival_PreparesOnce_BlocksActions_AndResumeSettlesOnlyCosmetics()
        {
            _settings.AnimationSettings.RewardFlightEnabled = true;
            _settings.AnimationSettings.RewardFlightDurationSeconds = 0.4f;
            _settings.AnimationSettings.RewardFlightFadeDurationSeconds = 0.08f;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            Sprite acceptedIcon = _reward.RewardIcon.sprite;
            ClickAction(_reward);
            Assert.That(_view.RewardArrival.IsPlaying, Is.True);
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
            int randomCalls = _wheelRandom.CallCount;
            ClickSpin();
            ClickBoardExit();
            ClickAction(_reward);
            Assert.That(_run.PendingSpin, Is.Null);
            Assert.That(_wheelRandom.CallCount, Is.EqualTo(randomCalls));
            AssertRewardSettledOnce(ticket);
            yield return new WaitForSecondsRealtime(0.08f);
            var copies = _view.RewardArrival.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            Assert.That(copies.Length, Is.EqualTo(3));
            Assert.That(copies.Any(copy => copy.gameObject.activeSelf), Is.True);
            Assert.That(copies.All(copy => copy.sprite == acceptedIcon), Is.True);
            _settings.AnimationSettings.RewardFlightDurationSeconds = 60f;
            _controller.Resume();
            Assert.That(_view.RewardArrival.IsPlaying, Is.False);
            Assert.That(copies.All(copy => !copy.gameObject.activeSelf && copy.sprite == null), Is.True);
            Assert.That(_reward.gameObject.activeSelf, Is.False);
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
            SpinTicket next = _run.PendingSpin;
            yield return new WaitForSecondsRealtime(0.55f);
            Assert.That(_run.PendingSpin, Is.SameAs(next), "An obsolete arrival must not complete a later action.");
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RewardArrival_DisablingOptionalView_ReleasesControlsWithoutGrantingAgain()
        {
            _settings.AnimationSettings.RewardFlightEnabled = true;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            ClickAction(_reward);
            Assert.That(_view.RewardArrival.IsPlaying, Is.True);
            _view.RewardArrival.enabled = false;
            yield return null;
            Assert.That(_view.RewardArrival.IsPlaying, Is.False);
            AssertRewardSettledOnce(ticket);
            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DisposeDuringArrival_CancelsCopiesAndKeepsTheBoardBlocked()
        {
            _settings.AnimationSettings.RewardFlightEnabled = true;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            ClickAction(_reward);
            Assert.That(_view.RewardArrival.IsPlaying, Is.True);
            _controller.Dispose();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(_view.RewardArrival.IsPlaying, Is.False);
            Assert.That(_view.RewardArrival.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .All(copy => !copy.gameObject.activeSelf), Is.True);
            ClickSpin();
            Assert.That(_run.PendingSpin, Is.Null);
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

        [TestCase(4, 5, ZoneType.Safe)]
        [TestCase(29, 30, ZoneType.Super)]
        [TestCase(30, 31, ZoneType.Normal)]
        [TestCase(59, 60, ZoneType.Super)]
        public void ZoneTransition_PreparesOnce_AndBlocksBothActions(int start, int expected, ZoneType type)
        {
            _controller.Dispose();
            _run = new RunSession(_settings, _wheelRandom, _landingRandom, start);
            _controller = new GameController(_settings, _run, _view, _reward, _exit, _bomb, _summary);
            _settings.AnimationSettings.ZoneTransitionSeconds = 0.12f;
            _settings.AnimationSettings.WheelCrossfadeSeconds = 0.08f;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            ClickAction(_reward);
            Assert.That(_run.ZoneNumber, Is.EqualTo(expected));
            Assert.That(_view.Wheel.BoundWheel, Is.SameAs(_run.Wheel));
            Assert.That(_run.Wheel.ZoneType, Is.EqualTo(type));
            Assert.That(_view.ZoneTransition.IsPlaying, Is.True);
            int randomCalls = _wheelRandom.CallCount;
            ClickSpin(); ClickBoardExit(); ClickAction(_reward);
            Assert.That(_run.PendingSpin, Is.Null);
            Assert.That(_wheelRandom.CallCount, Is.EqualTo(randomCalls));
            AssertRewardSettledOnce(ticket);
            _view.ZoneTransition.CompleteImmediately();
            Assert.That(_view.ZoneTransition.IsPlaying, Is.False);
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
        }

        [UnityTest]
        public IEnumerator Continue_AuthoredTimingUnlocksSpinAndExitSooner_WithoutEarlyInputOrAnotherGrant()
        {
            AnimationSettings authored = ReadField<GameSettings>(FindInScene<GameBootstrap>(), "_settings").AnimationSettings;
            _settings.AnimationSettings.RewardFlightEnabled = true;
            _settings.AnimationSettings.RewardFlightDurationSeconds = authored.RewardFlightDurationSeconds;
            _settings.AnimationSettings.RewardFlightStaggerSeconds = authored.RewardFlightStaggerSeconds;
            _settings.AnimationSettings.RewardFlightFadeDurationSeconds = authored.RewardFlightFadeDurationSeconds;
            _settings.AnimationSettings.ZoneTransitionSeconds = authored.ZoneTransitionSeconds;
            _settings.AnimationSettings.WheelCrossfadeSeconds = authored.WheelCrossfadeSeconds;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            ClickAction(_reward);
            var spin = _view.Wheel.transform.Find("ui_button_spin").GetComponent<UnityEngine.UI.Button>();
            var exit = _view.GetComponentsInChildren<ActionButtonView>().Single(b => b.name == "ui_button_exit");
            yield return new WaitForSecondsRealtime(.10f);
            Assert.That(spin.IsInteractable(), Is.False);
            Assert.That(exit.IsInteractable, Is.False);
            ClickSpin(); ClickBoardExit();
            Assert.That(_run.PendingSpin, Is.Null);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(spin.IsInteractable(), Is.True, "Continue should release the board within 0.35 seconds.");
            Assert.That(exit.IsInteractable, Is.True);
            AssertRewardSettledOnce(ticket);
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator ZoneTransition_UsesCapturedTiming_AndDoesNotReleaseInputBeforeFlight()
        {
            _settings.AnimationSettings.ZoneTransitionSeconds = 0.05f;
            _settings.AnimationSettings.WheelCrossfadeSeconds = 0.03f;
            _settings.AnimationSettings.RewardFlightEnabled = true;
            _settings.AnimationSettings.RewardFlightDurationSeconds = 0.22f;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume();
            ClickAction(_reward);
            _settings.AnimationSettings.ZoneTransitionSeconds = 60f;
            _settings.AnimationSettings.WheelCrossfadeSeconds = 60f;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(_view.ZoneTransition.IsPlaying, Is.False);
            Assert.That(_view.RewardArrival.IsPlaying, Is.True);
            ClickSpin(); ClickBoardExit();
            Assert.That(_run.PendingSpin, Is.Null);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(_view.RewardArrival.IsPlaying, Is.False);
            AssertRewardSettledOnce(ticket);
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
        }

        [UnityTest]
        public IEnumerator ZoneTransition_ResumeAndDisable_CompleteOnce_WhileDisposeCancels()
        {
            _settings.AnimationSettings.ZoneTransitionSeconds = 0.15f;
            _settings.AnimationSettings.WheelCrossfadeSeconds = 0.1f;
            SpinTicket ticket = StartRewardSpin();
            _controller.Resume(); ClickAction(_reward);
            _view.ZoneTransition.enabled = false;
            Assert.That(_view.ZoneTransition.IsPlaying, Is.False);
            AssertRewardSettledOnce(ticket);
            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
            ClickAction(_exit, "Go Back");
            _view.ZoneTransition.enabled = true;
            StartRewardSpin(); _controller.Resume(); ClickAction(_reward);
            Assert.That(_view.ZoneTransition.IsPlaying, Is.True);
            _controller.Resume();
            Assert.That(_view.ZoneTransition.IsPlaying, Is.False);
            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
            ClickAction(_exit, "Go Back");
            StartRewardSpin(); _controller.Resume(); ClickAction(_reward);
            _controller.Dispose();
            Assert.That(_view.ZoneTransition.IsPlaying, Is.False);
            yield return new WaitForSecondsRealtime(0.25f);
            ClickSpin();
            Assert.That(_run.PendingSpin, Is.Null);
        }

        [UnityTest]
        public IEnumerator ZoneTransition_ResizeRestoresVisuals_AndReleasesTheGate()
        {
            _settings.AnimationSettings.ZoneTransitionSeconds = 1f;
            _settings.AnimationSettings.WheelCrossfadeSeconds = 0.5f;
            StartRewardSpin(); _controller.Resume(); ClickAction(_reward);
            Assert.That(_view.ZoneTransition.IsPlaying, Is.True);
            var rect = (RectTransform)_view.transform;
            Vector2 original = rect.sizeDelta;
            rect.sizeDelta += Vector2.right * 20f;
            yield return null;
            Assert.That(_view.ZoneTransition.IsPlaying, Is.False);
            rect.sizeDelta = original;
            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
            Assert.That(_run.ZoneNumber, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ExitMotion_GatesEntranceAndCancelClose_WithoutRerolling()
        {
            _settings.AnimationSettings.ExitOpenSeconds = 0.07f;
            _settings.AnimationSettings.ExitCloseSeconds = 0.06f;
            WheelData prepared = _run.Wheel;
            int calls = _wheelRandom.CallCount;
            ClickBoardExit();
            ClickAction(_exit, "Go Back"); ClickAction(_exit, "Collect Rewards");
            Assert.That(_exit.IsReady, Is.False);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(_exit.IsReady, Is.True);
            ClickAction(_exit, "Go Back");
            Assert.That(_exit.IsClosing, Is.True);
            ClickSpin(); ClickBoardExit(); ClickAction(_exit, "Collect Rewards");
            Assert.That(_run.PendingSpin, Is.Null);
            Assert.That(_run.Summary, Is.Null);
            _settings.AnimationSettings.ExitCloseSeconds = 60f;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(_exit.gameObject.activeSelf, Is.False);
            Assert.That(_run.Wheel, Is.SameAs(prepared));
            Assert.That(_wheelRandom.CallCount, Is.EqualTo(calls));
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
        }

        [UnityTest]
        public IEnumerator CollectMotion_CommitsOnceBeforeClosing_AndResumeOpensTheSameSummary()
        {
            SettleRewardAndContinue();
            _settings.AnimationSettings.ExitCloseSeconds = 0.08f;
            long generation = _run.RunGeneration;
            ClickBoardExit(); ClickAction(_exit, "Collect Rewards");
            RunSummary collected = _run.Summary;
            Assert.That(_run.State, Is.EqualTo(RunState.CashedOut));
            Assert.That(_exit.IsClosing, Is.True);
            Assert.That(_summary.IsReady, Is.False);
            ClickSpin(); ClickAction(_exit, "Go Back"); ClickAction(_exit, "Collect Rewards");
            Assert.That(_run.Summary, Is.SameAs(collected));
            _controller.Resume();
            Assert.That(_exit.gameObject.activeSelf, Is.False);
            Assert.That(_summary.IsReady, Is.True);
            Assert.That(_run.Summary, Is.SameAs(collected));
            ClickAction(_summary);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_run.RunGeneration, Is.EqualTo(generation + 1));
            Assert.That(_summary.gameObject.activeSelf, Is.False);
            Assert.That(_run.ZoneNumber, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ChangedPolicy_ClosesToTheSamePreparedWheel_WithoutCollecting()
        {
            _settings.AnimationSettings.ExitCloseSeconds = 0.06f;
            WheelData prepared = _run.Wheel;
            ClickBoardExit();
            _settings.ZoneSettings.CashOutPolicy = CashOutPolicy.SafeAndSuperZonesOnly;
            ClickAction(_exit, "Collect Rewards");
            Assert.That(_exit.IsClosing, Is.True);
            ClickSpin();
            Assert.That(_run.PendingSpin, Is.Null);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(_exit.gameObject.activeSelf, Is.False);
            Assert.That(_run.Wheel, Is.SameAs(prepared));
            Assert.That(_run.Summary, Is.Null);
            ClickSpin();
            Assert.That(_run.State, Is.EqualTo(RunState.Spinning));
        }

        [UnityTest]
        public IEnumerator ExitClose_DisableReleasesNavigation_AndDisposeCancelsFutureNavigation()
        {
            _settings.AnimationSettings.ExitCloseSeconds = 0.08f;
            ClickBoardExit(); ClickAction(_exit, "Go Back");
            _exit.enabled = false;
            Assert.That(_exit.gameObject.activeSelf, Is.False);
            _exit.enabled = true;
            ClickBoardExit();
            Assert.That(_exit.IsReady, Is.True);
            ClickAction(_exit, "Collect Rewards");
            Assert.That(_exit.IsClosing, Is.True);
            _controller.Dispose();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_summary.gameObject.activeSelf, Is.False);
            ClickSpin();
            Assert.That(_run.PendingSpin, Is.Null);
        }

        [UnityTest]
        public IEnumerator SummaryEntrance_GatesRestart_UsesAcceptedTiming_AndResumeCannotGrantAgain()
        {
            SettleRewardAndContinue();
            _settings.AnimationSettings.SummaryEntranceSeconds = .10f;
            ClickBoardExit(); ClickAction(_exit, "Collect Rewards");
            RunSummary collected = _run.Summary;
            long generation = _run.RunGeneration;
            _settings.AnimationSettings.SummaryEntranceSeconds = 60f;
            Assert.That(_summary.IsReady, Is.False);
            ClickAction(_summary); ClickSpin(); ClickBoardExit();
            Assert.That(_run.RunGeneration, Is.EqualTo(generation));
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(_summary.IsReady, Is.True);
            _controller.Resume(); _controller.Resume();
            Assert.That(_run.Summary, Is.SameAs(collected));
            Assert.That(_run.State, Is.EqualTo(RunState.CashedOut));
            ClickAction(_summary); ClickAction(_summary);
            Assert.That(_run.RunGeneration, Is.EqualTo(generation + 1));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator SummaryResume_CompletesEntranceWithoutReplacingTheCollectedResult()
        {
            SettleRewardAndContinue();
            _settings.AnimationSettings.SummaryEntranceSeconds = 1f;
            ClickBoardExit(); ClickAction(_exit, "Collect Rewards");
            RunSummary collected = _run.Summary;
            Assert.That(_summary.IsReady, Is.False);
            _controller.Resume(); _controller.Resume();
            Assert.That(_summary.IsReady, Is.True);
            Assert.That(_run.Summary, Is.SameAs(collected));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(_run.Summary, Is.SameAs(collected));
        }

        [UnityTest]
        public IEnumerator BombEntrance_UsesAcceptedTiming_AndGatesRestartAndScrolling()
        {
            SettleRewardAndContinue();
            RewardAmount earned = _run.CreateSnapshot().Rewards[0];
            _settings.AnimationSettings.BombEntranceSeconds = 0.10f;
            _landingRandom.Sample = SampleFor(_run.Wheel, SlotOutcomeKind.Bomb);
            ClickSpin();
            _settings.AnimationSettings.BombEntranceSeconds = 60f;
            _controller.Resume();
            long generation = _run.RunGeneration;
            var list = _bomb.GetComponentInChildren<RewardCollectionView>();
            var scroll = list.GetComponent<UnityEngine.UI.ScrollRect>();
            Assert.That(_run.State, Is.EqualTo(RunState.Failed));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(_bomb.IsReady, Is.False);
            Assert.That(scroll.enabled, Is.False);
            ClickAction(_bomb); ClickSpin(); ClickBoardExit();
            Assert.That(_run.RunGeneration, Is.EqualTo(generation));
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.That(_bomb.IsReady, Is.True);
            Assert.That(scroll.enabled, Is.True);
            Assert.That(list.GetComponentInChildren<RewardItemView>().Quantity, Is.EqualTo(earned.Quantity));
            _settings.AnimationSettings.BombEntranceSeconds = 0f;
            ClickAction(_bomb); ClickAction(_bomb);
            Assert.That(_run.RunGeneration, Is.EqualTo(generation + 1));
            Assert.That(_run.State, Is.EqualTo(RunState.Ready));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator BombResume_CompletesOnlyCosmetics_AndCannotRefundTheLoss()
        {
            SettleRewardAndContinue();
            _settings.AnimationSettings.BombEntranceSeconds = 1f;
            _landingRandom.Sample = SampleFor(_run.Wheel, SlotOutcomeKind.Bomb);
            ClickSpin(); _controller.Resume();
            Assert.That(_bomb.IsReady, Is.False);
            long generation = _run.RunGeneration;
            _controller.Resume(); _controller.Resume();
            Assert.That(_bomb.IsReady, Is.True);
            Assert.That(_run.State, Is.EqualTo(RunState.Failed));
            Assert.That(_run.CreateSnapshot().Rewards, Is.Empty);
            Assert.That(_run.RunGeneration, Is.EqualTo(generation));
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(_bomb.GetComponentsInChildren<UiMotionView>().All(m => m.transform.localScale == Vector3.one), Is.True);
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
