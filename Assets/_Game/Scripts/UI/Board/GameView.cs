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
        private TMP_Text _zoneTypeLabel;

        [SerializeField]
        private TMP_Text _nextSafeLabel;

        [SerializeField]
        private TMP_Text _nextSuperLabel;

        [SerializeField]
        private TMP_Text _statusLabel;

        [SerializeField]
        private CanvasGroup _boardGroup;

        private ActionButtonView _listenedExitButton;

        public WheelView Wheel => _wheel;
        public WheelAnimator WheelAnimator => _wheelAnimator;

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
            _rewards.SetRewards(snapshot.Rewards, catalog, resetScroll);
            _zoneLabel.text = "ZONE " + snapshot.ZoneNumber.ToString(CultureInfo.InvariantCulture);
            _zoneTypeLabel.text = snapshot.Wheel.ZoneType == ZoneType.Super ? "SUPER ZONE"
                : snapshot.Wheel.ZoneType == ZoneType.Safe ? "SAFE ZONE" : "NORMAL ZONE";
            _nextSafeLabel.text = "SAFE ZONE " + NextSafeZone(snapshot.ZoneNumber, rules);
            _nextSuperLabel.text = "SUPER ZONE " + NextMilestone(snapshot.ZoneNumber, rules.SuperZoneInterval);
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
            _statusLabel.text = message ?? string.Empty;
            _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
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

        private void OnEnable()
        {
            if (_exitButton == null || !Application.IsPlaying(gameObject))
            {
                return;
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
            if (_wheel == null || _wheelAnimator == null || _exitButton == null || _rewards == null
                || _zoneLabel == null || _zoneTypeLabel == null || _nextSafeLabel == null
                || _nextSuperLabel == null || _statusLabel == null || _boardGroup == null)
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
