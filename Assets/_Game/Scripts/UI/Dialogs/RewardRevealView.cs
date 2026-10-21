using System;
using System.Globalization;
using DG.Tweening;
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

        [SerializeField]
        private CanvasGroup _iconGroup;

        [SerializeField]
        private CanvasGroup _textGroup;

        [SerializeField]
        private CanvasGroup _effectsGroup;

        [SerializeField]
        private CanvasGroup _continueGroup;

        private bool _actionPending = true;
        private ActionButtonView _listenedContinueButton;
        private Sequence _entrance;
        private Tween _shineTween;

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
            if (_dialog == null || _iconGroup == null || _textGroup == null
                || _effectsGroup == null || _continueGroup == null || _continueButton == null)
            {
                throw new InvalidOperationException("Reward reveal requires its authored motion groups and Continue button.");
            }
            ResetMotion();
            _icon.sprite = reward.Icon;
            _icon.preserveAspect = true;
            _name.text = reward.DisplayName;
            _quantity.text = "x" + quantity.ToString("N0", CultureInfo.InvariantCulture);
            BindEffect(_glow, settings.RewardGlowSprite);
            BindEffect(_shine, settings.RewardShineSprite);
            _actionPending = true;
            _continueButton.SetInteractable(false);
            float duration = settings.RewardRevealDurationSeconds;
            if (duration > 0f)
            {
                _iconGroup.alpha = _textGroup.alpha = _effectsGroup.alpha = _continueGroup.alpha = 0f;
                _icon.transform.localScale = Vector3.one * settings.RewardIconStartScale;
                ((RectTransform)_textGroup.transform).anchoredPosition = Vector2.down * settings.RewardTextOffset;
            }
            _dialog.Show(duration);
            StartShine(settings.RewardShineDegreesPerSecond);
            if (duration == 0f)
            {
                FinishEntrance();
                return;
            }

            // Relative timings keep one tunable total duration, including the action gate.
            Sequence entrance = DOTween.Sequence();
            entrance.Insert(0f, FadeIn(_effectsGroup, duration * 0.47f));
            entrance.Insert(duration * 0.125f, FadeIn(_iconGroup, duration * 0.25f));
            entrance.Insert(duration * 0.125f, _icon.transform.DOScale(settings.RewardIconPeakScale,
                duration * 0.5f).SetEase(Ease.OutCubic));
            entrance.Insert(duration * 0.625f, _icon.transform.DOScale(1f,
                duration * 0.25f).SetEase(Ease.InOutSine));
            entrance.Insert(duration * 0.31f, FadeIn(_textGroup, duration * 0.56f));
            RectTransform textMotion = (RectTransform)_textGroup.transform;
            entrance.Insert(duration * 0.31f, DOTween.To(() => textMotion.anchoredPosition,
                value => textMotion.anchoredPosition = value, Vector2.zero, duration * 0.56f).SetEase(Ease.OutCubic));
            entrance.Insert(duration * 0.75f, FadeIn(_continueGroup, duration * 0.25f));
            _entrance = entrance.SetUpdate(true).SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(FinishEntrance);
        }

        public void Hide()
        {
            _actionPending = true;
            ResetMotion();
            _dialog.Hide();
        }

        private void FinishEntrance()
        {
            _entrance = null;
            _actionPending = false;
            _continueButton.SetInteractable(true);
        }

        private static Tween FadeIn(CanvasGroup group, float duration) =>
            DOTween.To(() => group.alpha, value => group.alpha = value, 1f, duration);

        private void StartShine(float degreesPerSecond)
        {
            if (_shine == null || !_shine.enabled || degreesPerSecond == 0f)
            {
                return;
            }
            _shineTween = _shine.transform.DOLocalRotate(new Vector3(0f, 0f, 360f),
                360f / degreesPerSecond, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true)
                .SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        private void ResetMotion()
        {
            _entrance?.Kill(false);
            _entrance = null;
            _shineTween?.Kill(false);
            _shineTween = null;
            if (_iconGroup != null) _iconGroup.alpha = 1f;
            if (_textGroup != null)
            {
                _textGroup.alpha = 1f;
                ((RectTransform)_textGroup.transform).anchoredPosition = Vector2.zero;
            }
            if (_effectsGroup != null) _effectsGroup.alpha = 1f;
            if (_continueGroup != null) _continueGroup.alpha = 1f;
            if (_icon != null) _icon.transform.localScale = Vector3.one;
            if (_shine != null) _shine.transform.localRotation = Quaternion.identity;
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
            ResetMotion();
        }

        private void OnDestroy() => ResetMotion();

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
