using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Rewards;
using VertigoWheel.UI.Wheel;

namespace VertigoWheel.UI.Board
{
    /// <summary>Updates the board from a session snapshot without deciding gameplay outcomes.</summary>
    public sealed class GameView : MonoBehaviour
    {
        [SerializeField]
        private WheelView _wheel;

        [SerializeField]
        private WheelAnimator _wheelAnimator;

        [SerializeField]
        private ActionButtonView _exitButton;

        [SerializeField]
        private RewardCollectionView _rewards;

        [SerializeField]
        private TMP_Text _zoneLabel;

        [SerializeField]
        private TMP_Text _nextSafeLabel;

        [SerializeField]
        private TMP_Text _nextSuperLabel;

        [SerializeField]
        private TMP_Text _statusLabel;

        [SerializeField]
        private CanvasGroup _boardGroup;

        [SerializeField]
        private ZoneTrackView _zoneTrack;

        [SerializeField]
        private RewardArrivalView _rewardArrival;

        private ActionButtonView _listenedExitButton;
        private string _statusMessage = string.Empty;

        public WheelView Wheel => _wheel;
        public WheelAnimator WheelAnimator => _wheelAnimator;
        public RewardArrivalView RewardArrival => _rewardArrival;

        public event Action ExitRequested;

        public void Render(
            RunSnapshot snapshot,
            ZoneRules rules,
            IReadOnlyDictionary<RewardId, RewardSettings> catalog,
            bool modalOpen,
            bool resetScroll = false)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            ValidateReferences();
            _zoneTrack.Render(snapshot.ZoneNumber, rules);
            _rewards.SetRewards(snapshot.Rewards, catalog, resetScroll);
            RefreshWheelText();
            _nextSafeLabel.text = NextSafeZone(snapshot.ZoneNumber, rules);
            _nextSuperLabel.text = NextMilestone(snapshot.ZoneNumber, rules.SuperZoneInterval);
            _wheel.SetSpinInteractable(snapshot.CanSpin && !modalOpen);
            _exitButton.SetInteractable(snapshot.CanCashOut && !modalOpen);
            SetBoardBlocked(modalOpen);
        }

        public void SetStatus(string message)
        {
            if (_statusLabel == null)
            {
                throw new InvalidOperationException("The board is missing its status label reference.");
            }
            _statusMessage = message ?? string.Empty;
            RefreshStatus();
        }

        // Cosmetic asset edits are safe to preview immediately, including during an accepted spin.
        private void LateUpdate() => RefreshWheelText();

        private void RefreshWheelText()
        {
            if (_wheel == null)
            {
                return;
            }
            if (_zoneLabel != null)
            {
                if (_zoneLabel.text != _wheel.HeadingText)
                {
                    _zoneLabel.text = _wheel.HeadingText;
                }
                if (_zoneLabel.color != _wheel.HeadingColor)
                {
                    _zoneLabel.color = _wheel.HeadingColor;
                }
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null)
            {
                return;
            }
            string text = string.IsNullOrEmpty(_statusMessage) ? _wheel?.StatusText ?? string.Empty : _statusMessage;
            if (_statusLabel.text != text)
            {
                _statusLabel.text = text;
            }
            bool visible = !string.IsNullOrEmpty(text);
            if (_statusLabel.gameObject.activeSelf != visible)
            {
                _statusLabel.gameObject.SetActive(visible);
            }
        }

        public void SetBoardBlocked(bool blocked)
        {
            if (_boardGroup == null)
            {
                throw new InvalidOperationException("The board is missing its CanvasGroup reference.");
            }
            _boardGroup.interactable = !blocked;
            _boardGroup.blocksRaycasts = !blocked;
            _rewards?.SetInteractionEnabled(!blocked);
        }

        private void Awake() => CacheButtons();

        private void OnValidate() => CacheButtons();

        private void CacheButtons()
        {
            if (_exitButton == null)
            {
                _exitButton = transform.Find("ui_panel_run_rewards/ui_button_exit")?.GetComponent<ActionButtonView>();
            }
        }

        private void OnEnable()
        {
            CacheButtons();
            if (!Application.IsPlaying(gameObject))
            {
                return;
            }
            if (_exitButton == null)
            {
                throw new InvalidOperationException("The board is missing its Exit action button.");
            }
            _listenedExitButton = _exitButton;
            _listenedExitButton.Clicked += HandleExit;
        }

        private void OnDisable()
        {
            if (_listenedExitButton != null)
            {
                _listenedExitButton.Clicked -= HandleExit;
                _listenedExitButton = null;
            }
        }

        private void HandleExit()
        {
            ExitRequested?.Invoke();
        }

        private void ValidateReferences()
        {
            CacheButtons();
            if (_wheel == null || _wheelAnimator == null || _exitButton == null || _rewards == null
                || _zoneLabel == null || _nextSafeLabel == null
                || _nextSuperLabel == null || _statusLabel == null || _boardGroup == null || _zoneTrack == null)
            {
                throw new InvalidOperationException("The board is missing required wheel, controls, ledger, or label references.");
            }
        }

        private static string NextMilestone(int zoneNumber, int interval)
        {
            long nextZone = ((long)zoneNumber / interval + 1L) * interval;
            return nextZone <= int.MaxValue ? nextZone.ToString(CultureInfo.InvariantCulture) : "—";
        }

        private static string NextSafeZone(int zoneNumber, ZoneRules rules)
        {
            // Super zones replace safe zones where both intervals meet.
            if (rules.SafeZoneInterval % rules.SuperZoneInterval == 0)
            {
                return "—";
            }
            long nextZone = ((long)zoneNumber / rules.SafeZoneInterval + 1L) * rules.SafeZoneInterval;
            if (nextZone % rules.SuperZoneInterval == 0)
            {
                nextZone += rules.SafeZoneInterval;
            }
            return nextZone <= int.MaxValue ? nextZone.ToString(CultureInfo.InvariantCulture) : "—";
        }
    }
}
