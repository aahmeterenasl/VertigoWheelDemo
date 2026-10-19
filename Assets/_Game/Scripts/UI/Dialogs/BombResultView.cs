using System;
using UnityEngine;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;

namespace VertigoWheel.UI.Dialogs
{
    public sealed class BombResultView : MonoBehaviour
    {
        [SerializeField]
        private DialogView _dialog;

        [SerializeField]
        private UnityEngine.UI.Image _icon;

        [SerializeField]
        private ActionButtonView _restartButton;

        private bool _actionPending = true;

        public event Action RestartRequested;
        public bool IsReady => _dialog != null && _dialog.IsReady && !_actionPending;

        public void Show(Sprite bombIcon, AnimationSettings settings)
        {
            if (bombIcon == null || settings == null)
            {
                throw new ArgumentNullException("A bomb icon and animation settings are required.");
            }
            settings.Validate();
            _icon.sprite = bombIcon;
            _icon.preserveAspect = true;
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
