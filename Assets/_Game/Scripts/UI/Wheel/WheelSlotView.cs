using System.Globalization;
using TMPro;
using UnityEngine;

namespace VertigoWheel.UI.Wheel
{
    public sealed class WheelSlotView : MonoBehaviour
    {
        [SerializeField]
        private UnityEngine.UI.Image _icon;

        [SerializeField]
        private TMP_Text _quantity;

        public void Bind(Sprite icon, long quantity, bool isBomb)
        {
            _icon.sprite = icon;
            _icon.preserveAspect = true;
            _quantity.text = isBomb ? string.Empty : "x" + FormatQuantity(quantity);
        }

        public void SetLayout(Vector2 position, float size)
        {
            RectTransform root = (RectTransform)transform;
            root.anchoredPosition = position;
            root.sizeDelta = Vector2.one * size;
            _quantity.fontSize = size * 0.19f;
        }

        public void KeepUpright(float wheelRotationDegrees)
        {
            transform.localRotation = Quaternion.Euler(0, 0, -wheelRotationDegrees);
        }

        private static string FormatQuantity(long quantity)
        {
            if (quantity < 1000)
            {
                return quantity.ToString(CultureInfo.InvariantCulture);
            }
            double divisor = quantity < 1000000 ? 1000.0
                : quantity < 1000000000 ? 1000000.0
                : quantity < 1000000000000 ? 1000000000.0
                : quantity < 1000000000000000 ? 1000000000000.0
                : quantity < 1000000000000000000 ? 1000000000000000.0 : 1000000000000000000.0;
            string suffix = divisor == 1000 ? "K" : divisor == 1000000 ? "M"
                : divisor == 1000000000 ? "B" : divisor == 1000000000000 ? "T"
                : divisor == 1000000000000000 ? "Qa" : "Qi";
            return (quantity / divisor).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
        }
    }
}
