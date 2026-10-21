using VertigoWheel.UI.Common;
using TMPro;
using UnityEngine;

namespace VertigoWheel.UI.Wheel
{
    [ExecuteAlways]
    public sealed class WheelSlotView : MonoBehaviour
    {
        [SerializeField]
        private UnityEngine.UI.Image _icon;

        [SerializeField]
        private TMP_Text _quantity;

        [SerializeField, HideInInspector]
        private bool _isBomb;

        public void Bind(Sprite icon, long quantity, bool isBomb)
        {
            _isBomb = isBomb;
            _icon.sprite = icon;
            _icon.preserveAspect = true;
            _quantity.text = isBomb ? string.Empty : "x" + QuantityFormatter.Format(quantity, true);
            ApplyContentLayout();
        }

        public void SetLayout(Vector2 position, float size, float angleDegrees)
        {
            RectTransform root = (RectTransform)transform;
            root.anchoredPosition = position;
            root.sizeDelta = Vector2.one * size;
            // Face outward from the wheel center, then inherit the wheel's rotation.
            root.localRotation = Quaternion.Euler(0, 0, angleDegrees);
            _quantity.fontSize = size * 0.19f;
            _quantity.fontSizeMax = size * 0.19f;
            _quantity.fontSizeMin = size * 0.10f;
        }

        private void OnEnable()
        {
            ApplyContentLayout();
        }

        private void ApplyContentLayout()
        {
            if (_icon == null || _quantity == null)
            {
                return;
            }

            // Reward artwork reserves room below for its quantity; a bomb uses the slot center.
            RectTransform iconRect = _icon.rectTransform;
            Vector2 anchorMin = iconRect.anchorMin;
            Vector2 anchorMax = iconRect.anchorMax;
            anchorMin.y = _isBomb ? 0.26f : 0.335f;
            anchorMax.y = _isBomb ? 0.74f : 0.815f;
            iconRect.anchorMin = anchorMin;
            iconRect.anchorMax = anchorMax;
            _quantity.gameObject.SetActive(!_isBomb);
        }

    }
}
