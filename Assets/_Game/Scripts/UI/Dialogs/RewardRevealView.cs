using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;

namespace VertigoWheel.UI.Dialogs
{
    public sealed class RewardRevealView : MonoBehaviour
    {
        [SerializeField]
        private DialogView _dialog;

        [SerializeField]
        private UnityEngine.UI.Image _icon;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _quantity;

        [SerializeField]
        private UnityEngine.UI.Image _glow;

        [SerializeField]
        private UnityEngine.UI.Image _shine;

        [SerializeField]
        private ActionButtonView _continueButton;

        private bool _actionPending = true;

        public event Action ContinueRequested;
        public bool IsReady => _dialog != null && _dialog.IsReady && !_actionPending;

        public void Show(RewardSettings reward, long quantity, AnimationSettings settings)
        {
            if (reward == null || settings == null)
            {
                throw new ArgumentNullException("A reward and animation settings are required.");
            }
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }
            reward.Validate();
            settings.Validate();
            _icon.sprite = reward.Icon;
            _icon.preserveAspect = true;
            _name.text = reward.DisplayName;
            _quantity.text = "x" + quantity.ToString("N0", CultureInfo.InvariantCulture);
            BindEffect(_glow, settings.RewardGlowSprite);
            BindEffect(_shine, settings.RewardShineSprite);
            _actionPending = true;
            _continueButton.SetInteractable(true);
            _dialog.Show(settings.RewardRevealDurationSeconds, () => _actionPending = false);
        }

        public void Hide()
        {
            _actionPending = true;
            _dialog.Hide();
        }

        private static void BindEffect(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        private void OnEnable()
        {
            if (Application.IsPlaying(gameObject))
            {
                _continueButton.Clicked += Continue;
            }
        }

        private void OnDisable()
        {
            if (_continueButton != null)
            {
                _continueButton.Clicked -= Continue;
            }
            _actionPending = true;
        }

        private void Continue()
        {
            if (!IsReady)
            {
                return;
            }
            _actionPending = true;
            _continueButton.SetInteractable(false);
            ContinueRequested?.Invoke();
        }
    }
}
