using System;
using DG.Tweening;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Rewards
{
    /// <summary>Three decorative copies of an accepted reward; never owns reward totals or progression.</summary>
    public sealed class RewardArrivalView : MonoBehaviour
    {
        private const int CopyCount = 3;

        [SerializeField]
        private RewardCollectionView _rewards;

        [SerializeField]
        private UnityEngine.UI.Image[] _icons = Array.Empty<UnityEngine.UI.Image>();

        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _layer;
        private Sprite _sourceSprite;
        private Vector2 _sourceCenter;
        private Vector2 _sourceSize;
        private RewardId _rewardId;
        private float _elapsed;
        private float _duration;
        private float _stagger;
        private float _delay;
        private float _arcHeight;
        private float _startScale;
        private Tween _flight;
        private Action _completed;

        public bool IsPlaying => _completed != null;

        public bool TryCaptureSource(UnityEngine.UI.Image source)
        {
            if (IsPlaying) return false;
            _sourceSprite = null;
            _layer = transform as RectTransform;
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled || source.sprite == null
                || !HasUsableLayer() || !TryLocalRect(source.rectTransform, out Rect sourceRect))
            {
                return false;
            }
            // Normalized Canvas coordinates survive a resize even after the reveal is hidden.
            _sourceCenter = new Vector2((sourceRect.center.x - _layer.rect.xMin) / _layer.rect.width,
                (sourceRect.center.y - _layer.rect.yMin) / _layer.rect.height);
            _sourceSize = new Vector2(sourceRect.width / _layer.rect.width, sourceRect.height / _layer.rect.height);
            _sourceSprite = source.sprite;
            return true;
        }

        public bool TryPlay(RewardId rewardId, AnimationSettings settings, Action completed)
        {
            if (IsPlaying || !isActiveAndEnabled || settings == null || completed == null
                || !settings.RewardFlightEnabled || settings.RewardFlightDurationSeconds == 0f
                || _sourceSprite == null || _icons == null || _icons.Length != CopyCount || !HasUsableLayer())
            {
                return false;
            }
            foreach (UnityEngine.UI.Image icon in _icons)
                if (icon == null) return false;
            settings.Validate();
            _rewardId = rewardId;
            if (!TryDestination(out _, out _)) return false;
            _duration = settings.RewardFlightDurationSeconds;
            _stagger = settings.RewardFlightStaggerSeconds;
            _delay = settings.RewardFlightFadeDurationSeconds * 0.5f;
            _arcHeight = settings.RewardFlightArcHeight;
            _startScale = settings.RewardFlightStartScale;
            _elapsed = 0f;
            _completed = completed;
            foreach (UnityEngine.UI.Image icon in _icons)
            {
                icon.sprite = _sourceSprite;
                icon.preserveAspect = true;
                icon.gameObject.SetActive(false);
            }
            float total = Mathf.Max(settings.RewardFlightFadeDurationSeconds,
                _delay + _duration + _stagger * (CopyCount - 1));
            _flight = DOTween.To(() => _elapsed, value => _elapsed = value, total, total)
                .SetEase(Ease.Linear).SetUpdate(true).SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(Finish);
            return true;
        }

        public bool CompleteImmediately()
        {
            if (!IsPlaying) return false;
            Finish();
            return true;
        }

        public void Cancel()
        {
            _completed = null;
            Clear();
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (_flight == null || !_flight.IsActive() || !TryDestination(out Vector2 destination, out Vector2 endSize))
            {
                Finish();
                return;
            }
            Vector2 origin = _layer.rect.min + Vector2.Scale(_layer.rect.size, _sourceCenter);
            Vector2 startSize = Vector2.Scale(_layer.rect.size, _sourceSize) * _startScale;
            for (int index = 0; index < CopyCount; index++)
            {
                float phase = (_elapsed - _delay - index * _stagger) / _duration;
                UnityEngine.UI.Image icon = _icons[index];
                if (icon == null)
                {
                    Finish();
                    return;
                }
                bool visible = phase >= 0f && phase < 1f;
                icon.gameObject.SetActive(visible);
                if (!visible) continue;
                float eased = 1f - Mathf.Pow(1f - phase, 3f);
                Vector2 position = Vector2.Lerp(origin, destination, eased);
                position.y += Mathf.Sin(phase * Mathf.PI) * _arcHeight;
                icon.rectTransform.anchoredPosition = position;
                icon.rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, eased);
                icon.color = new Color(1f, 1f, 1f, Mathf.Min(1f, (1f - phase) * 5f));
            }
        }

        private bool TryDestination(out Vector2 position, out Vector2 size)
        {
            position = size = Vector2.zero;
            if (!HasUsableLayer() || _rewards == null
                || !_rewards.TryGetArrivalTarget(_rewardId, out RectTransform icon, out RectTransform viewport)
                || !TryLocalRect(icon, out Rect iconRect) || !TryLocalRect(viewport, out Rect viewportRect))
            {
                return false;
            }
            float left = Mathf.Max(iconRect.xMin, viewportRect.xMin);
            float right = Mathf.Min(iconRect.xMax, viewportRect.xMax);
            float bottom = Mathf.Max(iconRect.yMin, viewportRect.yMin);
            float top = Mathf.Min(iconRect.yMax, viewportRect.yMax);
            if (right > left && top > bottom)
            {
                position = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
            }
            else
            {
                // A clipped row targets its own nearest viewport edge, never another visible row.
                position = new Vector2(Mathf.Clamp(iconRect.center.x, viewportRect.xMin, viewportRect.xMax),
                    Mathf.Clamp(iconRect.center.y, viewportRect.yMin, viewportRect.yMax));
            }
            size = iconRect.size * 0.8f;
            return true;
        }

        private bool TryLocalRect(RectTransform rect, out Rect localRect)
        {
            localRect = default;
            Canvas sourceCanvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            Canvas targetCanvas = _layer.GetComponentInParent<Canvas>()?.rootCanvas;
            if (sourceCanvas == null || targetCanvas == null) return false;
            Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
            Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            rect.GetWorldCorners(_corners);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int index = 0; index < _corners.Length; index++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, _corners[index]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, targetCamera, out Vector2 point))
                    return false;
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            localRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return localRect.width > 0f && localRect.height > 0f;
        }

        private bool HasUsableLayer() => _layer != null && _layer.rect.width > 0f && _layer.rect.height > 0f;

        private void Finish()
        {
            Action completed = _completed;
            _completed = null;
            Clear();
            completed?.Invoke();
        }

        private void Clear()
        {
            Tween flight = _flight;
            _flight = null;
            flight?.Kill(false);
            _sourceSprite = null;
            if (_icons == null) return;
            foreach (UnityEngine.UI.Image icon in _icons)
            {
                if (icon == null) continue;
                icon.gameObject.SetActive(false);
                icon.sprite = null;
                icon.color = Color.white;
            }
        }

        private void OnDisable() => Finish();
        private void OnDestroy() => Finish();
    }
}
