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
        private ActionButtonView _listenedContinueButton;

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

        private void Awake() => CacheButtons();

        private void OnValidate() => CacheButtons();

        private void CacheButtons()
        {
            if (_continueButton == null)
            {
                _continueButton = transform.Find("ui_safe_area/ui_panel_dialog_content/ui_button_continue")
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
            if (_continueButton == null)
            {
                throw new InvalidOperationException("The reward reveal is missing its Continue action button.");
            }
            _listenedContinueButton = _continueButton;
            _listenedContinueButton.Clicked += Continue;
        }

        private void OnDisable()
        {
            if (_listenedContinueButton != null)
            {
                _listenedContinueButton.Clicked -= Continue;
                _listenedContinueButton = null;
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
