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

        private Tween _openingTween;
        private Action _shown;
        private int _showVersion;
        private bool _isOpen;
        private bool _isReady;

        public bool IsOpen => _isOpen;
        public bool IsReady => _isReady;

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

            CancelOpening();
            int version = _showVersion;
            _isOpen = true;
            _shown = shown;
            gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _group.interactable = false;
            _group.alpha = duration == 0f ? 1f : 0f;
            _content.localScale = duration == 0f ? Vector3.one : Vector3.one * 0.92f;
            if (duration == 0f)
            {
                FinishOpening(version);
                return;
            }

            Sequence opening = DOTween.Sequence();
            opening.Join(DOTween.To(() => _group.alpha, value => _group.alpha = value, 1f, duration));
            opening.Join(DOTween.To(() => _content.localScale, value => _content.localScale = value,
                Vector3.one, duration).SetEase(Ease.OutCubic));
            _openingTween = opening.SetUpdate(true).SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => FinishOpening(version));
        }

        public void Hide()
        {
            ResetView();
            gameObject.SetActive(false);
        }

        private void FinishOpening(int version)
        {
            if (version != _showVersion || !_isOpen || !isActiveAndEnabled)
            {
                return;
            }
            _openingTween = null;
            _group.alpha = 1f;
            _content.localScale = Vector3.one;
            _group.interactable = true;
            _isReady = true;
            Action shown = _shown;
            _shown = null;
            shown?.Invoke();
        }

        private void CancelOpening()
        {
            _showVersion++;
            _isReady = false;
            _shown = null;
            Tween opening = _openingTween;
            _openingTween = null;
            opening?.Kill(false);
        }

        private void ResetView()
        {
            CancelOpening();
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
        }

        private void OnDisable() => ResetView();
        private void OnDestroy() => CancelOpening();
    }
}
