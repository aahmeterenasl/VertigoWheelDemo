using System;
using DG.Tweening;
using UnityEngine;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Wheel
{
    public sealed class WheelAnimator : MonoBehaviour
    {
        [SerializeField]
        private WheelView _view;

        private Tween _spinTween;
        private SpinTicket _ticket;
        private Action<SpinTicket> _completed;
        private float _targetRotation;

        public bool IsSpinning => _ticket != null;

        public bool TryPlay(SpinTicket ticket, AnimationSettings settings, Action<SpinTicket> completed)
        {
            if (IsSpinning || !isActiveAndEnabled)
            {
                return false;
            }
            if (ticket == null || settings == null || completed == null)
            {
                throw new ArgumentNullException("A spin ticket, animation settings, and completion callback are required.");
            }
            CacheView();
            if (_view == null || !ReferenceEquals(_view.BoundWheel, ticket.Wheel))
            {
                throw new InvalidOperationException("Bind the ticket's prepared wheel before starting its animation.");
            }
            settings.Validate();
            // Read authored values once. Mid-spin Inspector edits affect a later spin.
            float duration = settings.SpinDurationSeconds;
            Ease ease = settings.SpinEase;
            _targetRotation = (float)WheelAngleCalculator.GetTargetRotationDegrees(
                _view.RotationDegrees, ticket.SelectedSlotIndex, _view.SlotZeroAngleDegrees,
                _view.ClockwiseSlotOrder, _view.PointerAngleDegrees, settings.FullTurns, settings.ClockwiseSpin);
            _ticket = ticket;
            _completed = completed;
            _view.SetSpinInteractable(false);
            _spinTween = DOTween.To(() => _view.RotationDegrees, _view.SetRotation, _targetRotation, duration)
                .SetEase(ease).SetUpdate(true).SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(Finish);
            return true;
        }

        /// <summary>Finish the accepted visual spin through the same once-only callback path on resume.</summary>
        public bool CompleteImmediately()
        {
            if (!IsSpinning)
            {
                return false;
            }
            _spinTween?.Kill(false);
            Finish();
            return true;
        }

        public void Cancel()
        {
            Tween tween = _spinTween;
            _spinTween = null;
            _ticket = null;
            _completed = null;
            tween?.Kill(false);
        }

        private void Finish()
        {
            SpinTicket ticket = _ticket;
            Action<SpinTicket> completed = _completed;
            if (ticket == null)
            {
                return;
            }
            _view.SetRotation(_targetRotation % 360f);
            _spinTween = null;
            _ticket = null;
            _completed = null;
            completed(ticket);
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
        private void OnValidate() => CacheView();

        private void CacheView()
        {
            if (_view == null)
            {
                _view = GetComponent<WheelView>();
            }
        }
    }
}
