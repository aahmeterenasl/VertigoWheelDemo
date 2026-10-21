using System;
using DG.Tweening;
using UnityEngine;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Wheel
{
    /// <summary>Owns pointer ticks; it never chooses or settles a spin.</summary>
    public sealed class WheelFeedbackView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _pointerMotion;

        private Tween _pointerTween;
        private bool _configured;
        private double _phaseOffset;
        private double _lastBand;
        private float _lastRotation;
        private float _pointerPhase;
        private float _deflection;
        private float _tickAngle;

        public bool TryBeginSpin(WheelView wheel, AnimationSettings settings)
        {
            if (wheel == null || settings == null)
            {
                throw new ArgumentNullException("Wheel and animation settings are required for feedback.");
            }
            ResetFeedback();
            if (!isActiveAndEnabled || !settings.WheelFeedbackEnabled)
            {
                return false;
            }
            if (_pointerMotion == null)
            {
                throw new InvalidOperationException("Wheel feedback needs its pointer child.");
            }
            _configured = true;
            _tickAngle = settings.PointerTickAngleDegrees;
            _phaseOffset = wheel.SlotZeroAngleDegrees - wheel.PointerAngleDegrees + 180.0 / WheelData.SlotCount;
            _lastRotation = wheel.RotationDegrees;
            _lastBand = BoundaryBand(_lastRotation);
            if (_tickAngle > 0f && settings.PointerTickDurationSeconds > 0f)
            {
                // Reuse one paused tween throughout the spin instead of allocating on every crossing.
                _pointerTween = DOTween.To(() => _pointerPhase, SetPointerPhase, 1f, settings.PointerTickDurationSeconds)
                    .SetEase(Ease.Linear).SetUpdate(true).SetAutoKill(false).Pause().SetTarget(this)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }
            return true;
        }

        public void UpdateSpin(float rotationDegrees)
        {
            if (!_configured || !isActiveAndEnabled)
            {
                return;
            }
            double band = BoundaryBand(rotationDegrees);
            float delta = rotationDegrees - _lastRotation;
            _lastRotation = rotationDegrees;
            if (band == _lastBand)
            {
                return;
            }
            _lastBand = band;
            // Coalesce skipped boundaries and let a running deflection return to neutral.
            if (_pointerTween != null && !_pointerTween.IsPlaying())
            {
                _deflection = -Mathf.Sign(delta) * _tickAngle;
                _pointerTween.Restart();
            }
        }

        public void ResetFeedback()
        {
            _configured = false;
            StopPointer();
        }

        private double BoundaryBand(float rotation) => Math.Floor((rotation + _phaseOffset) / (360.0 / WheelData.SlotCount));

        private void SetPointerPhase(float phase)
        {
            _pointerPhase = phase;
            _pointerMotion.localRotation = Quaternion.Euler(0f, 0f, _deflection * Mathf.Sin(phase * Mathf.PI));
        }

        private void StopPointer()
        {
            _pointerTween?.Kill(false);
            _pointerTween = null;
            _pointerPhase = 0f;
            if (_pointerMotion != null)
            {
                _pointerMotion.localRotation = Quaternion.identity;
            }
        }

        private void OnDisable() => ResetFeedback();
        private void OnDestroy() => ResetFeedback();
    }
}
