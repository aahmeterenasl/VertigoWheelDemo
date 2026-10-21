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

        [SerializeField] private UiMotionView _titleMotion;
        [SerializeField] private UiMotionView _actionMotion;
        private RunSummary _shownSummary;

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
            // Settings refreshes must not restart an entrance or interrupt inspection of the same result.
            if (_dialog.IsOpen && ReferenceEquals(_shownSummary, summary)) return;
            CompleteVisuals();
            _title.text = "YOU GOT:";
            _rewards.SetRewards(summary.Rewards, catalog, true);
            _shownSummary = summary;
            _rewards.SetInteractionEnabled(false);
            _actionPending = true;
            _restartButton.SetInteractable(true);
            float duration = summary.Rewards.Count == 0 ? 0f : settings.SummaryEntranceSeconds;
            _dialog.Show(duration, () =>
            {
                CompleteVisuals();
                _actionPending = false;
                _rewards.SetInteractionEnabled(true);
            }, keepBackdropVisible: true);
            if (duration > 0f)
            {
                _titleMotion?.Reveal(duration * 0.45f, 0f, 8f, 1f);
                _actionMotion?.Reveal(duration * 0.4f, duration * 0.5f, 8f, 1f);
                _rewards.RevealVisible(settings);
            }
        }

        public void CompleteEntrance()
        {
            CompleteVisuals();
            _dialog.CompleteOpening();
        }

        private void CompleteVisuals()
        {
            _titleMotion?.Complete();
            _actionMotion?.Complete();
            _rewards?.CompleteFeedback();
        }

        public void Hide()
        {
            CompleteVisuals();
            _shownSummary = null;
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
            CompleteVisuals();
            _shownSummary = null;
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
