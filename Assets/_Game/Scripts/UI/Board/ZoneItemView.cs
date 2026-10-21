using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace VertigoWheel.UI.Board
{
    public sealed class ZoneItemView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _number;

        internal float MeasureMinimumWidth(int digits, float minimumFontSize)
        {
            // Measure once per render, using bold digits to reserve room for the current zone too.
            float width = _number.GetPreferredValues("<b>" + new string('8', digits) + "</b>").x;
            return width * minimumFontSize / _number.fontSizeMax / 0.92f + 8f;
        }

        internal void SetMinimumFontSize(float size) => _number.fontSizeMin = size;

        public void Bind(int zoneNumber, bool isCurrent, Color numberColor)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber));
            }
            if (_number == null)
            {
                throw new InvalidOperationException("A zone item needs its number reference.");
            }
            _number.text = zoneNumber.ToString(CultureInfo.InvariantCulture);
            _number.color = numberColor;
            _number.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
