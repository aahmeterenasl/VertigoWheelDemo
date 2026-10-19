using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Rewards;

namespace VertigoWheel.UI.Dialogs
{
    public sealed class CashOutSummaryView : MonoBehaviour
    {
        [SerializeField]
        private DialogView _dialog;

        [SerializeField]
        private RewardCollectionView _rewards;

        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private ActionButtonView _restartButton;

        private bool _actionPending = true;

        public event Action RestartRequested;
        public bool IsReady => _dialog != null && _dialog.IsReady && !_actionPending;

        public void Show(RunSummary summary, IReadOnlyDictionary<RewardId, RewardSettings> catalog,
            AnimationSettings settings)
        {
            if (summary == null || catalog == null || settings == null)
            {
                throw new ArgumentNullException("A cash-out summary, reward catalog, and animation settings are required.");
            }
            settings.Validate();
            _title.text = "REWARDS COLLECTED";
            _rewards.SetRewards(summary.Rewards, catalog, true);
            _actionPending = true;
            _restartButton.SetInteractable(true);
            _dialog.Show(settings.DialogDurationSeconds, () => _actionPending = false);
        }

        public void Hide()
        {
            _actionPending = true;
            _dialog.Hide();
        }

        private void OnEnable()
        {
            if (Application.IsPlaying(gameObject))
            {
                _restartButton.Clicked += Restart;
            }
        }

        private void OnDisable()
        {
            if (_restartButton != null)
            {
                _restartButton.Clicked -= Restart;
            }
            _actionPending = true;
        }

        private void Restart()
        {
            if (!IsReady)
            {
                return;
            }
            _actionPending = true;
            _restartButton.SetInteractable(false);
            RestartRequested?.Invoke();
        }
    }
}
