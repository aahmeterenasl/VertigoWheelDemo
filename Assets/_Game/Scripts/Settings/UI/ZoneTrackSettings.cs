using System;
using UnityEngine;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "ZoneTrackSettings", menuName = "Vertigo Wheel/Settings/Zone Track")]
    public sealed class ZoneTrackSettings : ScriptableObject
    {
        [SerializeField]
        private Sprite _backgroundSprite;

        [SerializeField]
        private Sprite _currentBackgroundSprite;

        [SerializeField]
        private Color _normalColor = new Color(0.85f, 0.89f, 0.93f);

        [SerializeField]
        private Color _safeColor = new Color(0.48f, 0.94f, 0.38f);

        [SerializeField]
        private Color _superColor = new Color(1f, 0.77f, 0.16f);

        public Sprite BackgroundSprite
        {
            get => _backgroundSprite;
            set => _backgroundSprite = value;
        }

        public Sprite CurrentBackgroundSprite
        {
            get => _currentBackgroundSprite;
            set => _currentBackgroundSprite = value;
        }

        public Color NormalColor
        {
            get => _normalColor;
            set
            {
                ValidateColor(value);
                _normalColor = value;
            }
        }

        public Color SafeColor
        {
            get => _safeColor;
            set
            {
                ValidateColor(value);
                _safeColor = value;
            }
        }

        public Color SuperColor
        {
            get => _superColor;
            set
            {
                ValidateColor(value);
                _superColor = value;
            }
        }

        public void Validate()
        {
            if (_backgroundSprite == null || _currentBackgroundSprite == null)
            {
                throw new InvalidOperationException("The zone track needs background and current-zone sprites.");
            }
            ValidateColor(_normalColor);
            ValidateColor(_safeColor);
            ValidateColor(_superColor);
        }

        private static void ValidateColor(Color value)
        {
            for (int channel = 0; channel < 4; channel++)
            {
                float component = value[channel];
                if (float.IsNaN(component) || float.IsInfinity(component) || component < 0f || component > 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Zone colors must have finite channels between zero and one.");
                }
            }
        }
    }
}
