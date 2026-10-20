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

        private Sprite _backgroundSprite;
        private Sprite _currentBackgroundSprite;
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
            if (_items == null || _items.Length == 0)
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
            long first = Math.Max(1L, Math.Min((long)currentZone - _items.Length / 2,
                (long)int.MaxValue - _items.Length + 1L));
            for (int index = 0; index < _items.Length; index++)
            {
                int zone = (int)(first + index);
                ZoneType type = rules.GetZoneType(zone);
                Color color = type == ZoneType.Super ? _superColor : type == ZoneType.Safe ? _safeColor : _normalColor;
                bool isCurrent = zone == currentZone;
                _items[index].Bind(zone, isCurrent, isCurrent ? _currentBackgroundSprite : _backgroundSprite, color);
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
            _normalColor = _settings.NormalColor;
            _safeColor = _settings.SafeColor;
            _superColor = _settings.SuperColor;
            _hasStyle = true;
        }
    }
}
