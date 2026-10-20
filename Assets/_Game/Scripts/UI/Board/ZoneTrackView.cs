using System;
using UnityEngine;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Board
{
    /// <summary>Rebinds a fixed set of nearby-zone items; it never advances the run or creates items per zone.</summary>
    public sealed class ZoneTrackView : MonoBehaviour
    {
        [SerializeField]
        private ZoneTrackSettings _settings;

        [SerializeField]
        private ZoneItemView[] _items = Array.Empty<ZoneItemView>();

        [SerializeField]
        private UnityEngine.UI.Image _background;

        [SerializeField]
        private UnityEngine.UI.Image _currentBackground;

        private Sprite _backgroundSprite;
        private Sprite _currentBackgroundSprite;
        private Sprite _safeCurrentBackgroundSprite;
        private Sprite _superCurrentBackgroundSprite;
        private Color _currentNumberColor;
        private float _completedOpacity;
        private Color _normalColor;
        private Color _safeColor;
        private Color _superColor;
        private bool _hasStyle;

        public void Render(int currentZone, ZoneRules rules)
        {
            if (currentZone < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentZone));
            }
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (_items == null || _items.Length == 0 || _items.Length % 2 == 0
                || _background == null || _currentBackground == null)
            {
                throw new InvalidOperationException("The zone track needs settings and its reusable item references.");
            }
            foreach (ZoneItemView item in _items)
            {
                if (item == null)
                {
                    throw new InvalidOperationException("The zone track contains a missing item.");
                }
            }
            CaptureStyle();
            ZoneType currentType = rules.GetZoneType(currentZone);
            _background.sprite = _backgroundSprite;
            _currentBackground.sprite = currentType == ZoneType.Super ? _superCurrentBackgroundSprite
                : currentType == ZoneType.Safe ? _safeCurrentBackgroundSprite : _currentBackgroundSprite;
            _currentBackground.color = currentType == ZoneType.Super ? _superColor : Color.white;
            long first = (long)currentZone - _items.Length / 2;
            for (int index = 0; index < _items.Length; index++)
            {
                long candidate = first + index;
                bool visible = candidate >= 1 && candidate <= int.MaxValue;
                _items[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }
                int zone = (int)candidate;
                ZoneType type = rules.GetZoneType(zone);
                Color color = type == ZoneType.Super ? _superColor : type == ZoneType.Safe ? _safeColor : _normalColor;
                bool isCurrent = zone == currentZone;
                if (isCurrent && type != ZoneType.Safe)
                {
                    color = _currentNumberColor;
                }
                else if (zone < currentZone)
                {
                    color.a *= _completedOpacity;
                }
                _items[index].Bind(zone, isCurrent, color);
            }
        }

        private void CaptureStyle()
        {
            try
            {
                if (_settings == null)
                {
                    throw new InvalidOperationException("The zone track needs its settings asset.");
                }
                _settings.Validate();
            }
            catch (Exception exception) when (_hasStyle
                && (exception is ArgumentException || exception is InvalidOperationException))
            {
                // Incomplete Play-mode authoring must not interrupt an accepted spin or zone transition.
                // Keep the last valid style; a corrected asset is read on the next render.
                return;
            }
            _backgroundSprite = _settings.BackgroundSprite;
            _currentBackgroundSprite = _settings.CurrentBackgroundSprite;
            _safeCurrentBackgroundSprite = _settings.SafeCurrentBackgroundSprite;
            _superCurrentBackgroundSprite = _settings.SuperCurrentBackgroundSprite;
            _currentNumberColor = _settings.CurrentNumberColor;
            _completedOpacity = _settings.CompletedOpacity;
            _normalColor = _settings.NormalColor;
            _safeColor = _settings.SafeColor;
            _superColor = _settings.SuperColor;
            _hasStyle = true;
        }
    }
}
