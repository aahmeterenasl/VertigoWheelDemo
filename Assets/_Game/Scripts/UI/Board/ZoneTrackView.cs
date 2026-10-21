using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using VertigoWheel.UI.Common;
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

        [SerializeField] private RectTransform _currentFrame;

        [SerializeField] private UiMotionView _markerAccent;
        [SerializeField] private TMP_Text _departingNumber;
        [SerializeField] private UiMotionView _numberStrip;
        private long _departingZone;
        private Color _departingColor;
        private int _lastZone;
        private int _lastSafeInterval;
        private int _lastSuperInterval;
        private bool _canAnimateAdvance;
        private ZoneType _currentType;
        private int _visibleCount;
        private float _layoutWidth;
        private float _minimumNumberFontSize;
        private ZoneRules _lastRules;

        public bool AnimateAdvance(float duration, float accentScale)
        {
            if (!_canAnimateAdvance) return false;
            _canAnimateAdvance = false;
            float spacing = ((RectTransform)transform).rect.width / _visibleCount;
            _numberStrip?.Slide(duration, spacing);
            if (_departingNumber != null && _departingZone >= 1)
            {
                _departingNumber.text = _departingZone.ToString(CultureInfo.InvariantCulture);
                _departingNumber.color = _departingColor;
                _departingNumber.transform.parent.gameObject.SetActive(true);
            }
            if (_currentType != ZoneType.Normal) _markerAccent?.Pulse(duration, accentScale);
            return true;
        }

        public void CompleteMotion()
        {
            _numberStrip?.Complete();
            _markerAccent?.Complete();
            if (_departingNumber != null) _departingNumber.transform.parent.gameObject.SetActive(false);
        }

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
            float width = ((RectTransform)transform).rect.width;
            int digits = Math.Min(int.MaxValue, (long)currentZone + _items.Length / 2)
                .ToString(CultureInfo.InvariantCulture).Length;
            float minimumWidth = _items[_items.Length / 2].MeasureMinimumWidth(digits, _minimumNumberFontSize);
            int visibleCount = Mathf.Clamp(Mathf.FloorToInt(width / minimumWidth), 1, _items.Length);
            if (visibleCount % 2 == 0) visibleCount--;
            bool layoutChanged = visibleCount != _visibleCount || Mathf.Abs(width - _layoutWidth) > .1f;
            bool changed = currentZone != _lastZone || rules.SafeZoneInterval != _lastSafeInterval
                || rules.SuperZoneInterval != _lastSuperInterval;
            if (changed || layoutChanged)
            {
                CompleteMotion();
                _departingZone = (long)_lastZone - _visibleCount / 2;
                _canAnimateAdvance = !layoutChanged && _lastZone > 0 && (long)currentZone == (long)_lastZone + 1
                    && rules.SafeZoneInterval == _lastSafeInterval && rules.SuperZoneInterval == _lastSuperInterval;
            }
            _visibleCount = visibleCount;
            _layoutWidth = width;
            _lastRules = rules;
            ApplyWindowLayout();
            if (_canAnimateAdvance && _departingZone >= 1)
            {
                ZoneType type = rules.GetZoneType((int)_departingZone);
                _departingColor = type == ZoneType.Super ? _superColor : type == ZoneType.Safe ? _safeColor : _normalColor;
                _departingColor.a *= _completedOpacity;
            }
            ZoneType currentType = rules.GetZoneType(currentZone);
            _currentType = currentType;
            _lastZone = currentZone;
            _lastSafeInterval = rules.SafeZoneInterval;
            _lastSuperInterval = rules.SuperZoneInterval;
            _background.sprite = _backgroundSprite;
            _currentBackground.sprite = currentType == ZoneType.Super ? _superCurrentBackgroundSprite
                : currentType == ZoneType.Safe ? _safeCurrentBackgroundSprite : _currentBackgroundSprite;
            _currentBackground.color = currentType == ZoneType.Super ? _superColor : Color.white;
            long first = (long)currentZone - _items.Length / 2;
            for (int index = 0; index < _items.Length; index++)
            {
                long candidate = first + index;
                bool visible = Math.Abs(index - _items.Length / 2) <= _visibleCount / 2
                    && candidate >= 1 && candidate <= int.MaxValue;
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

        private void OnRectTransformDimensionsChange()
        {
            if (_lastRules != null && Mathf.Abs(((RectTransform)transform).rect.width - _layoutWidth) > .1f)
                Render(_lastZone, _lastRules);
        }

        private void ApplyWindowLayout()
        {
            for (int index = 0; index < _items.Length; index++)
            {
                float left = (float)(index - _items.Length / 2 + _visibleCount / 2) / _visibleCount;
                SetHorizontalAnchors((RectTransform)_items[index].transform, left, left + 1f / _visibleCount);
                _items[index].SetMinimumFontSize(_minimumNumberFontSize);
            }
            float markerLeft = .5f - .5f / _visibleCount;
            SetHorizontalAnchors(_currentBackground.rectTransform, markerLeft, 1f - markerLeft);
            if (_currentFrame != null) SetHorizontalAnchors(_currentFrame, markerLeft, 1f - markerLeft);
            if (_departingNumber != null)
            {
                SetHorizontalAnchors((RectTransform)_departingNumber.transform.parent, -1f / _visibleCount, 0f);
                _departingNumber.fontSizeMin = _minimumNumberFontSize;
            }
        }

        private static void SetHorizontalAnchors(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(left, rect.anchorMin.y);
            rect.anchorMax = new Vector2(right, rect.anchorMax.y);
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
            _minimumNumberFontSize = _settings.MinimumNumberFontSize;
            _normalColor = _settings.NormalColor;
            _safeColor = _settings.SafeColor;
            _superColor = _settings.SuperColor;
            _hasStyle = true;
        }
    }
}
