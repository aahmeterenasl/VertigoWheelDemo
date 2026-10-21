using System;
using UnityEngine;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;

namespace VertigoWheel.UI.Dialogs
{
    public sealed class ExitConfirmationView : MonoBehaviour
    {
        [SerializeField]
        private DialogView _dialog;

        [SerializeField]
        private ActionButtonView _collectButton;

        [SerializeField]
        private ActionButtonView _keepPlayingButton;

        [SerializeField] private UiMotionView _foreground;

        private bool _actionPending = true;
        private ActionButtonView _listenedCollectButton;
        private ActionButtonView _listenedKeepPlayingButton;

        public event Action CollectRequested;
        public event Action KeepPlayingRequested;
        public bool IsReady => _dialog != null && _dialog.IsReady && !_actionPending;
        public bool IsClosing => _dialog != null && _dialog.IsClosing;

        public void Show(AnimationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            settings.Validate();
            _actionPending = true;
            _collectButton.SetInteractable(true);
            _keepPlayingButton.SetInteractable(true);
            float duration = settings.ExitOpenSeconds;
            _dialog.Show(duration, () => _actionPending = false);
            _foreground?.Reveal(duration * (4f / 7f), duration * (3f / 7f), 0f, settings.ExitStartScale);
        }

        public void Close(float duration, Action closed)
        {
            _actionPending = true;
            _collectButton.SetInteractable(false);
            _keepPlayingButton.SetInteractable(false);
            _dialog.Close(duration, closed);
        }

        public void CompleteClose() => _dialog.CompleteClose();

        public void Hide()
        {
            _actionPending = true;
            _foreground?.Complete();
            _dialog.Hide();
        }

        private void Awake() => CacheButtons();

        private void OnValidate() => CacheButtons();

        private void CacheButtons()
        {
            if (_collectButton == null)
            {
                _collectButton = transform.Find("ui_safe_area/ui_panel_dialog_content/ui_animation_exit_content/ui_button_collect_rewards")
                    ?.GetComponent<ActionButtonView>();
            }
            if (_keepPlayingButton == null)
            {
                _keepPlayingButton = transform.Find("ui_safe_area/ui_panel_dialog_content/ui_animation_exit_content/ui_button_keep_playing")
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
            if (_collectButton == null || _keepPlayingButton == null)
            {
                throw new InvalidOperationException("The exit confirmation is missing its Collect or Keep Playing action button.");
            }
            _listenedCollectButton = _collectButton;
            _listenedKeepPlayingButton = _keepPlayingButton;
            _listenedCollectButton.Clicked += Collect;
            _listenedKeepPlayingButton.Clicked += KeepPlaying;
        }

        private void OnDisable()
        {
            _dialog?.CompleteClose();
            _foreground?.Complete();
            if (_listenedCollectButton != null)
            {
                _listenedCollectButton.Clicked -= Collect;
                _listenedCollectButton = null;
            }
            if (_listenedKeepPlayingButton != null)
            {
                _listenedKeepPlayingButton.Clicked -= KeepPlaying;
                _listenedKeepPlayingButton = null;
            }
            _actionPending = true;
        }

        private void Collect()
        {
            if (ConsumeAction())
            {
                CollectRequested?.Invoke();
            }
        }

        private void KeepPlaying()
        {
            if (ConsumeAction())
            {
                KeepPlayingRequested?.Invoke();
            }
        }

        private bool ConsumeAction()
        {
            if (!IsReady)
            {
                return false;
            }
            _actionPending = true;
            _collectButton.SetInteractable(false);
            _keepPlayingButton.SetInteractable(false);
            return true;
        }
    }
}
