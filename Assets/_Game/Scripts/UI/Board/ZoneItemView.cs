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

        [SerializeField]
        private UnityEngine.UI.Image _background;

        public void Bind(int zoneNumber, bool isCurrent, Sprite background, Color numberColor)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber));
            }
            if (background == null)
            {
                throw new ArgumentNullException(nameof(background));
            }
            if (_number == null || _background == null)
            {
                throw new InvalidOperationException("A zone item needs its number and background references.");
            }
            _number.text = zoneNumber.ToString(CultureInfo.InvariantCulture);
            _number.color = numberColor;
            _number.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
            _background.sprite = background;
        }
    }
}
