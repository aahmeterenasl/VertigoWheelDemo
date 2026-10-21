using System;
using System.Collections.Generic;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Rewards;

namespace VertigoWheel.UI.Dialogs
{
    public sealed class BombResultView : MonoBehaviour
    {
        [SerializeField]
        private DialogView _dialog;

        [SerializeField]
        private UnityEngine.UI.Image _icon;

        [SerializeField]
        private RewardCollectionView _rewards;

        [SerializeField]
        private ActionButtonView _restartButton;

        [SerializeField] private UiMotionView _impact;
        [SerializeField] private UiMotionView _halo;
        [SerializeField] private UiMotionView _text;
        [SerializeField] private UiMotionView _actions;

        private bool _actionPending = true;
        private ActionButtonView _listenedRestartButton;

        public event Action RestartRequested;
        public bool IsReady => _dialog != null && _dialog.IsReady && !_actionPending;

        public void Show(Sprite bombIcon, IReadOnlyList<RewardAmount> lostRewards,
            IReadOnlyDictionary<RewardId, RewardSettings> catalog, AnimationSettings settings)
        {
            if (bombIcon == null || lostRewards == null || catalog == null || settings == null)
            {
                throw new ArgumentNullException("A bomb icon, lost rewards, catalog, and animation settings are required.");
            }
            settings.Validate();
            _icon.sprite = bombIcon;
            _icon.preserveAspect = true;
            _rewards.SetRewards(lostRewards, catalog, !_dialog.IsOpen);
            _rewards.SetInteractionEnabled(false);
            _actionPending = true;
            _restartButton.SetInteractable(true);
            float duration = settings.BombEntranceSeconds;
            _dialog.Show(duration, () =>
            {
                _actionPending = false;
                _rewards.SetInteractionEnabled(true);
            });
            _impact?.Reveal(duration * 0.8f, 0f, 0f, settings.BombStartScale);
            _halo?.Pulse(duration * 0.375f, settings.BombHaloScale);
            _text?.Reveal(duration * 0.55f, duration * 0.1f, 8f, 1f);
            _actions?.Reveal(duration * 0.45f, duration * 0.5f, 8f, 1f);
        }

        public void CompleteEntrance()
        {
            ResetMotion();
            _dialog.CompleteOpening();
        }

        private void ResetMotion()
        {
            _impact?.Complete();
            _halo?.Complete();
            _text?.Complete();
            _actions?.Complete();
        }

        public void Hide()
        {
            _actionPending = true;
            ResetMotion();
            _rewards.SetInteractionEnabled(false);
            _dialog.Hide();
        }

        private void Awake() => CacheButtons();

        private void OnValidate() => CacheButtons();

        private void CacheButtons()
        {
            if (_restartButton == null)
            {
                _restartButton = transform.Find("ui_safe_area/ui_panel_dialog_content/ui_animation_bomb_actions/ui_button_restart")
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
                throw new InvalidOperationException("The bomb result is missing its Restart action button.");
            }
            _listenedRestartButton = _restartButton;
            _listenedRestartButton.Clicked += Restart;
        }

        private void OnDisable()
        {
            ResetMotion();
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
