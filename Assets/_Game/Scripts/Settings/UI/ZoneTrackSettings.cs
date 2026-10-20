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
        private Sprite _safeCurrentBackgroundSprite;

        [SerializeField]
        private Sprite _superCurrentBackgroundSprite;

        [SerializeField]
        private Color _currentNumberColor = Color.black;

        [SerializeField, Range(0f, 1f)]
        private float _completedOpacity = 0.35f;

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

        public Sprite SafeCurrentBackgroundSprite
        {
            get => _safeCurrentBackgroundSprite;
            set => _safeCurrentBackgroundSprite = value;
        }

        public Sprite SuperCurrentBackgroundSprite
        {
            get => _superCurrentBackgroundSprite;
            set => _superCurrentBackgroundSprite = value;
        }

        public Color CurrentNumberColor
        {
            get => _currentNumberColor;
            set
            {
                ValidateColor(value);
                _currentNumberColor = value;
            }
        }

        public float CompletedOpacity
        {
            get => _completedOpacity;
            set
            {
                ValidateOpacity(value);
                _completedOpacity = value;
            }
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
            if (_backgroundSprite == null || _currentBackgroundSprite == null
                || _safeCurrentBackgroundSprite == null || _superCurrentBackgroundSprite == null)
            {
                throw new InvalidOperationException("The zone track needs background and current-zone sprites.");
            }
            ValidateColor(_normalColor);
            ValidateColor(_safeColor);
            ValidateColor(_superColor);
            ValidateColor(_currentNumberColor);
            ValidateOpacity(_completedOpacity);
        }

        private static void ValidateOpacity(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Completed-zone opacity must be between zero and one.");
            }
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
