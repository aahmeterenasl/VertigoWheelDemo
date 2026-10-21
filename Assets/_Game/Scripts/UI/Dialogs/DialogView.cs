using System;
using DG.Tweening;
using UnityEngine;

namespace VertigoWheel.UI.Dialogs
{
    /// <summary>Owns the overlay animation and input gate; it never changes run state.</summary>
    public sealed class DialogView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _group;

        [SerializeField]
        private RectTransform _content;

        [SerializeField]
        private CanvasGroup _backdrop;

        private Tween _openingTween;
        private Action _shown;
        private Action _closed;
        private bool _closing;
        private int _showVersion;
        private bool _isOpen;
        private bool _isReady;

        public bool IsOpen => _isOpen;
        public bool IsReady => _isReady;
        public bool IsClosing => _closing;

        public void Show(float duration, Action shown = null)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }
            if (_group == null || _content == null)
            {
                throw new InvalidOperationException("A dialog requires its CanvasGroup and content reference.");
            }

            CancelTransition();
            int version = _showVersion;
            _isOpen = true;
            _shown = shown;
            gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _group.interactable = false;
            // An optional backdrop lets a specialized view stage its own foreground.
            CanvasGroup fadingGroup = _backdrop != null ? _backdrop : _group;
            _group.alpha = 1f;
            fadingGroup.alpha = duration == 0f ? 1f : 0f;
            _content.localScale = duration == 0f || _backdrop != null ? Vector3.one : Vector3.one * 0.92f;
            if (duration == 0f)
            {
                FinishOpening(version);
                return;
            }

            Sequence opening = DOTween.Sequence();
            if (_backdrop != null)
            {
                opening.Append(DOTween.To(() => fadingGroup.alpha, value => fadingGroup.alpha = value, 1f, duration * 0.5f));
                opening.AppendInterval(duration * 0.5f);
            }
            else
            {
                opening.Join(DOTween.To(() => fadingGroup.alpha, value => fadingGroup.alpha = value, 1f, duration));
                opening.Join(DOTween.To(() => _content.localScale, value => _content.localScale = value,
                    Vector3.one, duration).SetEase(Ease.OutCubic));
            }
            _openingTween = opening.SetUpdate(true).SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => FinishOpening(version));
        }

        public void Hide()
        {
            ResetView();
            gameObject.SetActive(false);
        }

        public void Hide(float duration) => Close(duration, null);

        /// <summary>Normal navigation keeps its input blocker until the close callback is delivered.</summary>
        public void Close(float duration, Action closed)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (duration == 0f || !_isOpen || !isActiveAndEnabled)
            {
                Hide();
                closed?.Invoke();
                return;
            }
            CancelTransition();
            int version = _showVersion;
            _closed = closed;
            _closing = true;
            _group.interactable = false;
            _openingTween = DOTween.To(() => _group.alpha, value => _group.alpha = value, 0f, duration)
                .SetUpdate(true).SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => FinishClose(version));
        }

        public void CompleteOpening()
        {
            if (!_isOpen || _closing || _isReady) return;
            _openingTween?.Kill(false);
            _openingTween = null;
            FinishOpening(_showVersion);
        }

        public void CompleteClose()
        {
            if (_closing) FinishClose(_showVersion);
        }

        private void FinishClose(int version)
        {
            if (version != _showVersion || !_closing) return;
            Action closed = _closed;
            _closed = null;
            _closing = false;
            Hide();
            closed?.Invoke();
        }

        private void FinishOpening(int version)
        {
            if (version != _showVersion || !_isOpen || !isActiveAndEnabled)
            {
                return;
            }
            _openingTween = null;
            _group.alpha = 1f;
            if (_backdrop != null) _backdrop.alpha = 1f;
            _content.localScale = Vector3.one;
            _group.interactable = true;
            _isReady = true;
            Action shown = _shown;
            _shown = null;
            shown?.Invoke();
        }

        private void CancelTransition()
        {
            _showVersion++;
            _isReady = false;
            _shown = null;
            _closed = null;
            _closing = false;
            Tween opening = _openingTween;
            _openingTween = null;
            opening?.Kill(false);
        }

        private void ResetView()
        {
            CancelTransition();
            _isOpen = false;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.interactable = false;
                _group.blocksRaycasts = false;
            }
            if (_content != null)
            {
                _content.localScale = Vector3.one;
            }
            if (_backdrop != null) _backdrop.alpha = 1f;
        }

        private void OnDisable()
        {
            // A disappearing optional view must not strand normal navigation in the controller.
            if (_closing) CompleteClose();
            else ResetView();
        }
        private void OnDestroy() => CancelTransition();
    }
}
