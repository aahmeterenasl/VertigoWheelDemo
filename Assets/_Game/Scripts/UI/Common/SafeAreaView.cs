using UnityEngine;

namespace VertigoWheel.UI.Common
{
    /// <summary>Insets a full-screen content container while its parent's background stays full-screen.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaView : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastDisplaySize;
        private bool _hasApplied;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            _hasApplied = false;
            Refresh();
        }

        private void Update() => Refresh();

        public void Refresh()
        {
            Rect safeArea = Screen.safeArea;
            Vector2Int displaySize = new Vector2Int(Screen.width, Screen.height);
            if (_hasApplied && safeArea == _lastSafeArea && displaySize == _lastDisplaySize)
            {
                return;
            }
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }
            Rect normalized = CalculateNormalizedArea(safeArea, displaySize);
            _rect.anchorMin = normalized.min;
            _rect.anchorMax = normalized.max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _lastSafeArea = safeArea;
            _lastDisplaySize = displaySize;
            _hasApplied = true;
        }

        /// <summary>Screen pixels become anchors, so CanvasScaler scaling does not change the inset.</summary>
        public static Rect CalculateNormalizedArea(Rect safeArea, Vector2Int displaySize)
        {
            if (displaySize.x <= 0 || displaySize.y <= 0 || !IsFinite(safeArea.x)
                || !IsFinite(safeArea.y) || !IsFinite(safeArea.width) || !IsFinite(safeArea.height)
                || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }
            float left = Mathf.Clamp01(safeArea.xMin / displaySize.x);
            float bottom = Mathf.Clamp01(safeArea.yMin / displaySize.y);
            float right = Mathf.Clamp01(safeArea.xMax / displaySize.x);
            float top = Mathf.Clamp01(safeArea.yMax / displaySize.y);
            return right > left && top > bottom
                ? Rect.MinMaxRect(left, bottom, right, top)
                : new Rect(0f, 0f, 1f, 1f);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
