using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Common
{
    /// <summary>Cosmetic feedback only; the stationary Button owns input and dispatches its action.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ButtonFeedbackView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler, ISubmitHandler
    {
        [SerializeField] private UnityEngine.UI.Button _button;
        [SerializeField] private RectTransform _visual;
        [SerializeField] private AnimationSettings _settings;

        private Tween _motion;
        private bool _pressed;
        private int _pointerId;
        private float _pressSeconds;
        private float _releaseSeconds;
        private float _pressedScale;

        private void Awake() => CacheReferences();
        private void OnValidate() => CacheReferences();

        private void CacheReferences()
        {
            if (_button == null) _button = GetComponent<UnityEngine.UI.Button>();
            if (_visual == null)
            {
                foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.name != "ui_animation_button_content") continue;
                    _visual = child;
                    break;
                }
            }
        }

        private bool CanInteract => isActiveAndEnabled && _button != null
            && _button.isActiveAndEnabled && _button.IsInteractable();

        public void OnPointerDown(PointerEventData data)
        {
            if (_pressed || data.button != PointerEventData.InputButton.Left || !CaptureSettings()) return;
            _pressed = true;
            _pointerId = data.pointerId;
            Animate(_pressedScale, _pressSeconds);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && data.pointerId == _pointerId) Release();
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (data.pointerId == _pointerId) Release();
        }

        public void OnCancel(BaseEventData data) => ResetVisual();

        public void OnSubmit(BaseEventData data)
        {
            if (_pressed || !CaptureSettings()) return;
            ResetVisual();
            if (_pressSeconds + _releaseSeconds <= 0f) return;
            Own(DOTween.Sequence()
                .Append(_visual.DOScale(_pressedScale, _pressSeconds).SetEase(Ease.OutCubic))
                .Append(_visual.DOScale(1f, _releaseSeconds).SetEase(Ease.OutCubic)));
        }

        private bool CaptureSettings()
        {
            CacheReferences();
            if (!CanInteract || _visual == null || _settings == null) return false;
            // Capture one press/release pair, so live Inspector edits affect the next interaction.
            _pressSeconds = _settings.ButtonPressSeconds;
            _releaseSeconds = _settings.ButtonReleaseSeconds;
            _pressedScale = _settings.ButtonPressedScale;
            // An invalid in-progress Inspector edit should skip cosmetics, not intercept the Button action.
            return IsDuration(_pressSeconds) && IsDuration(_releaseSeconds)
                && _pressedScale >= 0.9f && _pressedScale <= 1f;
        }

        private static bool IsDuration(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            if (!CanInteract) ResetVisual();
            else Animate(1f, _releaseSeconds);
        }

        private void Animate(float scale, float duration)
        {
            _motion?.Kill(false);
            _motion = null;
            if (_visual == null) return;
            if (duration <= 0f) _visual.localScale = Vector3.one * scale;
            else Own(_visual.DOScale(scale, duration).SetEase(Ease.OutCubic));
        }

        private void Own(Tween tween)
        {
            _motion?.Kill(false);
            _motion = tween.SetUpdate(true).SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => _motion = null);
        }

        private void ResetVisual()
        {
            _pressed = false;
            _motion?.Kill(false);
            _motion = null;
            if (_visual != null) _visual.localScale = Vector3.one;
        }

        private void LateUpdate()
        {
            if ((_pressed || _motion != null) && !CanInteract) ResetVisual();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) ResetVisual(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetVisual(); }
        private void OnRectTransformDimensionsChange() => ResetVisual();
        private void OnDisable() => ResetVisual();
        private void OnDestroy() => ResetVisual();
    }
}
