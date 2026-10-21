using DG.Tweening;
using UnityEngine;

namespace VertigoWheel.UI.Common
{
    /// <summary>Cosmetic motion on a dedicated child, independent of its parent's layout and hit area.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiMotionView : MonoBehaviour
    {
        private RectTransform _rect;
        private CanvasGroup _group;
        private Tween _motion;

        public void Reveal(float duration, float delay = 0f, float lift = 12f, float scale = 0.97f)
        {
            Complete();
            if (duration <= 0f || !isActiveAndEnabled) return;
            _group.alpha = 0f;
            _rect.anchoredPosition = Vector2.down * lift;
            _rect.localScale = Vector3.one * scale;
            Sequence sequence = DOTween.Sequence();
            sequence.AppendInterval(delay);
            sequence.Append(DOTween.To(() => _group.alpha, value => _group.alpha = value, 1f, duration));
            sequence.Join(DOTween.To(() => _rect.anchoredPosition, value => _rect.anchoredPosition = value, Vector2.zero, duration).SetEase(Ease.OutCubic));
            sequence.Join(_rect.DOScale(1f, duration).SetEase(Ease.OutCubic));
            Own(sequence);
        }

        public void Pulse(float duration, float scale)
        {
            Complete();
            if (duration <= 0f || !isActiveAndEnabled) return;
            Sequence sequence = DOTween.Sequence();
            sequence.Append(_rect.DOScale(scale, duration * 0.4f).SetEase(Ease.OutQuad));
            sequence.Append(_rect.DOScale(1f, duration * 0.6f).SetEase(Ease.OutCubic));
            Own(sequence);
        }

        public void Slide(float duration, float horizontalOffset)
        {
            Complete();
            if (duration <= 0f || !isActiveAndEnabled) return;
            _rect.anchoredPosition = Vector2.right * horizontalOffset;
            Own(DOTween.To(() => _rect.anchoredPosition, value => _rect.anchoredPosition = value,
                Vector2.zero, duration).SetEase(Ease.OutCubic));
        }

        public void Complete()
        {
            _motion?.Kill(false);
            _motion = null;
            if (_rect == null) _rect = (RectTransform)transform;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _rect.anchoredPosition = Vector2.zero;
            _rect.localScale = Vector3.one;
            _group.alpha = 1f;
        }

        private void Own(Tween tween)
        {
            _motion = tween.SetUpdate(true).SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => _motion = null);
        }

        private void OnDisable() => Complete();
        private void OnDestroy() => _motion?.Kill(false);
        private void OnRectTransformDimensionsChange()
        {
            if (_motion != null) Complete();
        }
    }
}
