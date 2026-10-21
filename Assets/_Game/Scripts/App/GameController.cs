using System;
using System.Collections.Generic;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Dialogs;

namespace VertigoWheel.App
{
    /// <summary>Coordinates one session and its views; only RunSession can settle rewards or advance a zone.</summary>
    public sealed class GameController : IDisposable
    {
        private readonly GameSettings _settings;
        private readonly RunSession _run;
        private readonly GameView _view;
        private readonly RewardRevealView _rewardReveal;
        private readonly ExitConfirmationView _exitConfirmation;
        private readonly BombResultView _bombResult;
        private readonly CashOutSummaryView _cashOutSummary;
        private readonly Dictionary<RewardId, RewardSettings> _catalog = new Dictionary<RewardId, RewardSettings>();
        private readonly Dictionary<RewardId, RewardSettings> _sourceRewards = new Dictionary<RewardId, RewardSettings>();
        private AnimationSettings _uiAnimation;
        private Sprite _acceptedBombIcon;
        private IReadOnlyList<RewardAmount> _lostRewards = Array.Empty<RewardAmount>();

        private ActiveModal _modal;
        private SpinTicket _activeTicket;
        private WheelData _boundWheel;
        private RunSnapshot _lastSnapshot;
        private ZoneRules _lastRules;
        private bool _disposed;
        private ZoneSettings _observedZoneSettings;
        private int _observedSafeInterval;
        private int _observedSuperInterval;
        private int _observedTierInterval;
        private long _observedMaximumQuantity;
        private CashOutPolicy _observedCashOutPolicy;
        private bool _hasObservedRules;

        public GameController(GameSettings settings, RunSession run, GameView view,
            RewardRevealView reward, ExitConfirmationView exit, BombResultView bomb,
            CashOutSummaryView summary)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _rewardReveal = reward != null ? reward : throw new ArgumentNullException(nameof(reward));
            _exitConfirmation = exit != null ? exit : throw new ArgumentNullException(nameof(exit));
            _bombResult = bomb != null ? bomb : throw new ArgumentNullException(nameof(bomb));
            _cashOutSummary = summary != null ? summary : throw new ArgumentNullException(nameof(summary));
            if (_run.State != RunState.Ready || _view.Wheel == null || _view.WheelAnimator == null)
            {
                throw new InvalidOperationException("Start a controller with a ready session and the board's wheel references.");
            }

            ValidateAnimation();
            if (_settings.BombIcon == null)
            {
                throw new InvalidOperationException("Game settings needs the supplied bomb icon.");
            }
            HideDialogs();
            BindPreparedWheel();
            Render(true);
            _view.SetStatus(string.Empty);
            CaptureAnimation();
            Subscribe();
        }

        /// <summary>Refreshes rule-dependent controls after an Inspector edit without regenerating the prepared wheel.</summary>
        public void RefreshReadyState()
        {
            if (_disposed || _modal != ActiveModal.None || _run.State != RunState.Ready || !RulesChanged())
            {
                return;
            }
            Execute(() =>
            {
                Render();
                _view.SetStatus(string.Empty);
            });
            ObserveRules();
        }

        /// <summary>Completes an accepted spin through its original once-only settlement path after app suspension.</summary>
        public void Resume()
        {
            if (_disposed || _run.State != RunState.Spinning
                || !ReferenceEquals(_run.PendingSpin, _activeTicket))
            {
                return;
            }
            if (!_view.WheelAnimator.CompleteImmediately())
            {
                HandleSpinCompleted(_activeTicket);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_view != null)
            {
                if (_view.Wheel != null)
                {
                    _view.Wheel.SpinRequested -= HandleSpin;
                    _view.Wheel.SetSpinInteractable(false);
                }
                _view.ExitRequested -= HandleExit;
                if (_view.WheelAnimator != null)
                {
                    _view.WheelAnimator.Cancel();
                }
                _view.SetBoardBlocked(true);
            }
            if (_rewardReveal != null)
            {
                _rewardReveal.ContinueRequested -= HandleContinue;
            }
            if (_exitConfirmation != null)
            {
                _exitConfirmation.CollectRequested -= HandleCollect;
                _exitConfirmation.KeepPlayingRequested -= HandleKeepPlaying;
            }
            if (_bombResult != null)
            {
                _bombResult.RestartRequested -= HandleRestart;
            }
            if (_cashOutSummary != null)
            {
                _cashOutSummary.RestartRequested -= HandleRestart;
            }
            if (_uiAnimation != null)
            {
                UnityEngine.Object.Destroy(_uiAnimation);
                _uiAnimation = null;
            }
            foreach (RewardSettings reward in _catalog.Values)
            {
                UnityEngine.Object.Destroy(reward);
            }
            _catalog.Clear();
            _sourceRewards.Clear();
            _acceptedBombIcon = null;
            _lostRewards = Array.Empty<RewardAmount>();
            _activeTicket = null;
            HideDialogs();
        }

        private void Subscribe()
        {
            _view.Wheel.SpinRequested += HandleSpin;
            _view.ExitRequested += HandleExit;
            _rewardReveal.ContinueRequested += HandleContinue;
            _exitConfirmation.CollectRequested += HandleCollect;
            _exitConfirmation.KeepPlayingRequested += HandleKeepPlaying;
            _bombResult.RestartRequested += HandleRestart;
            _cashOutSummary.RestartRequested += HandleRestart;
        }

        private void HandleSpin()
        {
            if (_disposed || _modal != ActiveModal.None || !_run.CanSpin
                || !_view.WheelAnimator.isActiveAndEnabled || _view.WheelAnimator.IsSpinning)
            {
                return;
            }
            Execute(() =>
            {
                CaptureAnimation();
                _settings.CreateZoneRules();
                ValidatePreparedVisuals();
                if (!_run.TryStartSpin(out SpinTicket ticket))
                {
                    return;
                }
                _activeTicket = ticket;
                _acceptedBombIcon = _settings.BombIcon;
                _view.SetStatus(string.Empty);
                Render();
                if (!_view.WheelAnimator.TryPlay(ticket, _uiAnimation, HandleSpinCompleted))
                {
                    HandleSpinCompleted(ticket);
                }
            });
        }

        private void HandleSpinCompleted(SpinTicket ticket)
        {
            if (_disposed || _modal != ActiveModal.None || ticket == null
                || !ReferenceEquals(_activeTicket, ticket))
            {
                return;
            }
            // Keep display-only history before Core clears the ledger on a bomb.
            IReadOnlyList<RewardAmount> lostRewards = ticket.Outcome.OutcomeKind == SlotOutcomeKind.Bomb
                ? _run.CreateSnapshot().Rewards : Array.Empty<RewardAmount>();
            if (!_run.TryResolveSpin(ticket))
            {
                return;
            }
            Execute(() =>
            {
                if (ticket.Outcome.OutcomeKind == SlotOutcomeKind.Bomb)
                {
                    _lostRewards = lostRewards;
                    _run.TryCompleteResolution(ticket);
                    _modal = ActiveModal.Bomb;
                    Render();
                    _bombResult.Show(_acceptedBombIcon, _lostRewards, _catalog, _uiAnimation);
                    return;
                }

                _modal = ActiveModal.Reward;
                Render();
                ShowReward();
            });
        }

        private void HandleContinue()
        {
            // Each dialog consumes its own ready action before raising this event.
            // Modal identity and the exact Core ticket are the controller's second gate.
            if (_disposed || _modal != ActiveModal.Reward || _run.State != RunState.Resolving
                || !ReferenceEquals(_activeTicket, _run.PendingSpin))
            {
                return;
            }
            Execute(() =>
            {
                CaptureAnimation();
                ValidateNextWheelSources();
                if (!_run.TryCompleteResolution(_activeTicket))
                {
                    return;
                }
                _activeTicket = null;
                _rewardReveal.Hide();
                _view.SetStatus(string.Empty);
                if (_run.State == RunState.CashedOut)
                {
                    ShowSummary(_run.Summary);
                    return;
                }
                BindPreparedWheel();
                _modal = ActiveModal.None;
                Render();
            }, ShowReward);
        }

        private void HandleExit()
        {
            if (_disposed || _modal != ActiveModal.None || _run.State != RunState.Ready)
            {
                return;
            }
            Execute(() =>
            {
                if (!_run.CanCashOut)
                {
                    Render();
                    return;
                }
                CaptureAnimation();
                _modal = ActiveModal.Exit;
                _view.SetStatus(string.Empty);
                Render();
                _exitConfirmation.Show(_uiAnimation);
            });
        }

        private void HandleKeepPlaying()
        {
            if (_disposed || _modal != ActiveModal.Exit || _run.State != RunState.Ready)
            {
                return;
            }
            _exitConfirmation.Hide();
            _modal = ActiveModal.None;
            Execute(Render);
        }

        private void HandleCollect()
        {
            if (_disposed || _modal != ActiveModal.Exit || _run.State != RunState.Ready)
            {
                return;
            }
            Execute(() =>
            {
                CaptureAnimation();
                if (!_run.TryCashOut(out RunSummary summary))
                {
                    _exitConfirmation.Hide();
                    _modal = ActiveModal.None;
                    Render();
                    _view.SetStatus("Exit is available in safe and super zones.");
                    return;
                }
                _exitConfirmation.Hide();
                _view.SetStatus(string.Empty);
                ShowSummary(summary);
            }, () => _exitConfirmation.Show(_uiAnimation));
        }

        private void HandleRestart()
        {
            bool terminalModal = (_modal == ActiveModal.Bomb && _run.State == RunState.Failed)
                || (_modal == ActiveModal.Summary && _run.State == RunState.CashedOut);
            if (_disposed || !terminalModal)
            {
                return;
            }
            Execute(() =>
            {
                CaptureAnimation();
                ValidateWheelSources(1);
                _run.Restart();
                _view.WheelAnimator.Cancel();
                _activeTicket = null;
                BindPreparedWheel();
                HideDialogs();
                _lostRewards = Array.Empty<RewardAmount>();
                _modal = ActiveModal.None;
                _view.SetStatus(string.Empty);
                Render(true);
            }, ReopenTerminalDialog);
        }

        private void BindPreparedWheel()
        {
            if (ReferenceEquals(_boundWheel, _run.Wheel))
            {
                return;
            }
            // Snapshot display metadata at the same boundary as the prepared wheel.
            // Incomplete Inspector edits cannot break a settled reward's list/reveal.
            // Keep earned IDs even if a later pool edit removes them.
            IReadOnlyDictionary<RewardId, RewardSettings> current = _settings.CreateRewardCatalog();
            foreach (KeyValuePair<RewardId, RewardSettings> entry in current)
            {
                _sourceRewards[entry.Key] = entry.Value;
                if (!_catalog.TryGetValue(entry.Key, out RewardSettings visual))
                {
                    visual = ScriptableObject.CreateInstance<RewardSettings>();
                    visual.hideFlags = HideFlags.HideAndDontSave;
                    _catalog.Add(entry.Key, visual);
                }
                visual.RewardId = entry.Key;
                visual.DisplayName = entry.Value.DisplayName;
                visual.Icon = entry.Value.Icon;
            }
            WheelSettings profile = _settings.GetWheelSettingsForZone(_run.ZoneNumber);
            _view.Wheel.Bind(_run.Wheel, profile, _catalog, _settings.BombIcon);
            _boundWheel = _run.Wheel;
        }

        private void Render() => Render(false);

        private void Render(bool resetScroll)
        {
            // An accepted outcome can finish even while a developer has an
            // incomplete Inspector edit. Ready actions validate the current rules.
            ZoneRules rules = _run.State == RunState.Ready || _lastRules == null
                ? _settings.CreateZoneRules() : _lastRules;
            RunSnapshot snapshot = _run.CreateSnapshot();
            _view.Render(snapshot, rules, _catalog, _modal != ActiveModal.None, resetScroll);
            _lastRules = rules;
            _lastSnapshot = snapshot;
            ObserveRules();
        }

        private void ShowReward()
        {
            if (_run.State == RunState.Resolving && _activeTicket != null)
            {
                _rewardReveal.Show(_catalog[_activeTicket.Outcome.RewardId],
                    _activeTicket.Outcome.Quantity, _uiAnimation);
            }
        }

        private void ShowSummary(RunSummary summary)
        {
            _modal = ActiveModal.Summary;
            Render();
            _cashOutSummary.Show(summary, _catalog, _uiAnimation);
        }

        private void ReopenTerminalDialog()
        {
            if (_modal == ActiveModal.Bomb)
            {
                _bombResult.Show(_acceptedBombIcon, _lostRewards, _catalog, _uiAnimation);
            }
            else if (_modal == ActiveModal.Summary)
            {
                _cashOutSummary.Show(_run.Summary, _catalog, _uiAnimation);
            }
        }

        private void HideDialogs()
        {
            if (_rewardReveal != null)
            {
                _rewardReveal.Hide();
            }
            if (_exitConfirmation != null)
            {
                _exitConfirmation.Hide();
            }
            if (_bombResult != null)
            {
                _bombResult.Hide();
            }
            if (_cashOutSummary != null)
            {
                _cashOutSummary.Hide();
            }
        }

        private void ValidateAnimation()
        {
            if (_settings.AnimationSettings == null)
            {
                throw new InvalidOperationException("Game settings needs animation settings.");
            }
            _settings.AnimationSettings.Validate();
        }

        private void CaptureAnimation()
        {
            ValidateAnimation();
            if (_uiAnimation == null)
            {
                _uiAnimation = ScriptableObject.CreateInstance<AnimationSettings>();
                _uiAnimation.hideFlags = HideFlags.HideAndDontSave;
            }
            // This private runtime snapshot is only for an accepted visual action;
            // developers continue tuning the actual authored Settings asset.
            AnimationSettings source = _settings.AnimationSettings;
            _uiAnimation.SpinDurationSeconds = source.SpinDurationSeconds;
            _uiAnimation.FullTurns = source.FullTurns;
            _uiAnimation.ClockwiseSpin = source.ClockwiseSpin;
            _uiAnimation.SpinEase = source.SpinEase;
            _uiAnimation.DialogDurationSeconds = source.DialogDurationSeconds;
            _uiAnimation.RewardRevealDurationSeconds = source.RewardRevealDurationSeconds;
            _uiAnimation.RewardShineDegreesPerSecond = source.RewardShineDegreesPerSecond;
            _uiAnimation.RewardIconStartScale = source.RewardIconStartScale;
            _uiAnimation.RewardIconPeakScale = source.RewardIconPeakScale;
            _uiAnimation.RewardTextOffset = source.RewardTextOffset;
            _uiAnimation.RewardGlowSprite = source.RewardGlowSprite;
            _uiAnimation.RewardShineSprite = source.RewardShineSprite;
            _uiAnimation.WheelFeedbackEnabled = source.WheelFeedbackEnabled;
            _uiAnimation.PointerTickAngleDegrees = source.PointerTickAngleDegrees;
            _uiAnimation.PointerTickDurationSeconds = source.PointerTickDurationSeconds;
        }

        private void ValidatePreparedVisuals()
        {
            foreach (WheelSlotData slot in _run.Wheel.Slots)
            {
                if (slot.OutcomeKind == SlotOutcomeKind.Bomb)
                {
                    if (_settings.BombIcon == null)
                    {
                        throw new InvalidOperationException("Game settings needs the supplied bomb icon.");
                    }
                    continue;
                }
                if (!_sourceRewards.TryGetValue(slot.RewardId, out RewardSettings reward) || reward == null
                    || reward.RewardId != slot.RewardId)
                {
                    throw new InvalidOperationException("The prepared reward's visual identity has changed: " + slot.RewardId);
                }
                reward.Validate();
            }
        }

        private void ValidateNextWheelSources()
        {
            if (_run.ZoneNumber < int.MaxValue)
            {
                ValidateWheelSources(_run.ZoneNumber + 1);
            }
        }

        private void ValidateWheelSources(int zoneNumber)
        {
            // Check the visual graph before Core commits the next prepared zone.
            // A failed edit leaves Resolving and its already-settled reward intact.
            _settings.CreateRewardCatalog();
            WheelSettings profile = _settings.GetWheelSettingsForZone(zoneNumber);
            ZoneType type = _settings.CreateZoneRules().GetZoneType(zoneNumber);
            if (type == ZoneType.Normal && _settings.BombIcon == null)
            {
                throw new InvalidOperationException("Game settings needs the supplied bomb icon.");
            }
            if (profile.Pool == null)
            {
                profile.Validate(type);
            }
            else
            {
                profile.ValidatePool();
            }
        }

        private void Execute(Action action, Action recoverDialog = null)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                // Invalid developer edits retain the last valid screen and Core data.
                // An already-settled reward can retry preparing the next zone.
                if (_lastSnapshot != null && _lastRules != null)
                {
                    _view.Render(_lastSnapshot, _lastRules, _catalog, _modal != ActiveModal.None);
                }
                _view.SetStatus(exception.Message);
                if (recoverDialog != null)
                {
                    try
                    {
                        recoverDialog();
                    }
                    catch (Exception recoveryException) when (recoveryException is ArgumentException
                        || recoveryException is InvalidOperationException)
                    {
                        _view.SetStatus(recoveryException.Message);
                    }
                }
            }
        }

        private bool RulesChanged()
        {
            ZoneSettings zones = _settings.ZoneSettings;
            return !_hasObservedRules || zones != _observedZoneSettings || zones == null
                || zones.SafeZoneInterval != _observedSafeInterval
                || zones.SuperZoneInterval != _observedSuperInterval
                || zones.RewardTierInterval != _observedTierInterval
                || zones.MaximumRewardQuantity != _observedMaximumQuantity
                || zones.CashOutPolicy != _observedCashOutPolicy;
        }

        private void ObserveRules()
        {
            _observedZoneSettings = _settings.ZoneSettings;
            _hasObservedRules = true;
            if (_observedZoneSettings == null)
            {
                return;
            }
            _observedSafeInterval = _observedZoneSettings.SafeZoneInterval;
            _observedSuperInterval = _observedZoneSettings.SuperZoneInterval;
            _observedTierInterval = _observedZoneSettings.RewardTierInterval;
            _observedMaximumQuantity = _observedZoneSettings.MaximumRewardQuantity;
            _observedCashOutPolicy = _observedZoneSettings.CashOutPolicy;
        }

        private enum ActiveModal
        {
            None,
            Reward,
            Exit,
            Bomb,
            Summary
        }
    }
}
