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

        private bool _actionPending = true;

        public event Action CollectRequested;
        public event Action KeepPlayingRequested;
        public bool IsReady => _dialog != null && _dialog.IsReady && !_actionPending;

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
            _dialog.Show(settings.DialogDurationSeconds, () => _actionPending = false);
        }

        public void Hide()
        {
            _actionPending = true;
            _dialog.Hide();
        }

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
            {
                return;
            }
            _collectButton.Clicked += Collect;
            _keepPlayingButton.Clicked += KeepPlaying;
        }

        private void OnDisable()
        {
            if (_collectButton != null)
            {
                _collectButton.Clicked -= Collect;
            }
            if (_keepPlayingButton != null)
            {
                _keepPlayingButton.Clicked -= KeepPlaying;
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
