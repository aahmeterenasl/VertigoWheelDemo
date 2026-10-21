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
        private ActionButtonView _listenedRestartButton;

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
            _title.text = "YOU GOT:";
            _rewards.SetRewards(summary.Rewards, catalog, !_dialog.IsOpen);
            _rewards.SetInteractionEnabled(false);
            _actionPending = true;
            _restartButton.SetInteractable(true);
            _dialog.Show(settings.DialogDurationSeconds, () =>
            {
                _actionPending = false;
                _rewards.SetInteractionEnabled(true);
            });
        }

        public void Hide()
        {
            _actionPending = true;
            _rewards.SetInteractionEnabled(false);
            _dialog.Hide();
        }

        private void Awake() => CacheButtons();

        private void OnValidate() => CacheButtons();

        private void CacheButtons()
        {
            if (_restartButton == null)
            {
                _restartButton = transform.Find("ui_safe_area/ui_panel_dialog_content/ui_button_restart")
                    ?.GetComponent<ActionButtonView>();
            }
        }

        private void OnEnable()
        {
            CacheButtons();
            if (!Application.IsPlaying(gameObject))
            {
                return;
            }
            if (_restartButton == null)
            {
                throw new InvalidOperationException("The cash-out summary is missing its Restart action button.");
            }
            _listenedRestartButton = _restartButton;
            _listenedRestartButton.Clicked += Restart;
        }

        private void OnDisable()
        {
            if (_rewards != null)
            {
                _rewards.SetInteractionEnabled(false);
            }
            if (_listenedRestartButton != null)
            {
                _listenedRestartButton.Clicked -= Restart;
                _listenedRestartButton = null;
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
