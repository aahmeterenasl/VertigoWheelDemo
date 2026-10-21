using System;
using DG.Tweening;
using UnityEngine;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;

namespace VertigoWheel.UI.Board
{
    /// <summary>Fades in prepared board visuals as the reward reveal fades out; never advances the run.</summary>
    public sealed class ZoneTransitionView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] _fadingGroups = Array.Empty<CanvasGroup>();
        [SerializeField] private ZoneTrackView _track;
        [SerializeField] private UiMotionView _safeAccent;
        [SerializeField] private UiMotionView _superAccent;

        private Tween _transition;
        private Action _changeView;
        private Action _completed;
        private float _elapsed;
        private float _fadeDuration;
        private float _slideDuration;
        private float _accentScale;
        private ZoneType _nextType;
        private int _version;

        public bool IsPlaying => _completed != null;

        public void Play(ZoneType nextType, AnimationSettings settings, Action changeView, Action completed)
        {
            if (settings == null || changeView == null || completed == null)
                throw new ArgumentNullException("Zone transitions need settings and both callbacks.");
            settings.Validate();
            Cancel();
            _nextType = nextType;
            _fadeDuration = settings.WheelCrossfadeSeconds;
            _slideDuration = settings.ZoneTransitionSeconds;
            _accentScale = settings.MilestonePulseScale;
            _changeView = changeView;
            _completed = completed;
            _elapsed = 0f;
            float duration = Mathf.Max(_fadeDuration, _slideDuration);
            if (duration <= 0f || !isActiveAndEnabled)
            {
                CompleteImmediately();
                return;
            }
            int version = _version;
            SetAlpha(_fadeDuration <= 0f ? 1f : 0f);
            // Bind synchronously at the accepted action boundary. A later Inspector edit cannot
            // invalidate a delayed binding or leave Core Ready with an old interactive wheel.
            ApplyChange();
            if (version != _version || !IsPlaying) return;
            _transition = DOTween.To(() => _elapsed, value =>
            {
                if (version != _version) return;
                _elapsed = value;
                float alpha = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(value / _fadeDuration);
                SetAlpha(alpha);
            }, duration, duration).SetEase(Ease.Linear).SetUpdate(true).SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(CompleteImmediately);
        }

        public void CompleteImmediately()
        {
            if (!IsPlaying) return;
            _transition?.Kill(false);
            _transition = null;
            int version = _version;
            ApplyChange();
            if (version != _version) return;
            Action completed = _completed;
            _completed = null;
            ResetVisuals();
            completed?.Invoke();
        }

        public void Cancel()
        {
            _version++;
            _changeView = null;
            _completed = null;
            _transition?.Kill(false);
            _transition = null;
            ResetVisuals();
        }

        private void ApplyChange()
        {
            if (_changeView == null) return;
            Action change = _changeView;
            _changeView = null;
            int version = _version;
            change();
            if (version != _version || !isActiveAndEnabled) return;
            bool advanced = _track != null && _track.AnimateAdvance(_slideDuration, _accentScale);
            if (!advanced) return;
            if (_nextType == ZoneType.Safe) _safeAccent?.Pulse(_slideDuration, _accentScale);
            if (_nextType == ZoneType.Super) _superAccent?.Pulse(_slideDuration, _accentScale);
        }

        private void SetAlpha(float alpha)
        {
            foreach (CanvasGroup group in _fadingGroups) if (group != null) group.alpha = alpha;
        }

        private void ResetVisuals()
        {
            SetAlpha(1f);
            _track?.CompleteMotion();
            _safeAccent?.Complete();
            _superAccent?.Complete();
        }

        private void OnDisable() => CompleteImmediately();
        private void OnDestroy() => Cancel();
        private void OnRectTransformDimensionsChange()
        {
            if (IsPlaying) CompleteImmediately();
        }
    }
}
