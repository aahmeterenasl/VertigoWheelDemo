using UnityEngine;

namespace VertigoWheel.UI.Common
{
    [ExecuteAlways, RequireComponent(typeof(UnityEngine.UI.GridLayoutGroup))]
    public sealed class ResponsiveGridView : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private float _minimumCellWidth = 200;

        [SerializeField, Min(0.01f)]
        private float _cellAspectRatio = 0.8f;

        private UnityEngine.UI.GridLayoutGroup _grid;

        private void OnEnable() => Refresh();
        private void OnRectTransformDimensionsChange() => Refresh();
        private void OnValidate() => Refresh();

        private void Refresh()
        {
            if (_grid == null)
            {
                _grid = GetComponent<UnityEngine.UI.GridLayoutGroup>();
            }
            if (_grid == null || _minimumCellWidth <= 0 || _cellAspectRatio <= 0)
            {
                return;
            }
            float width = ((RectTransform)transform).rect.width - _grid.padding.horizontal;
            if (width <= 0)
            {
                return;
            }
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + _grid.spacing.x)
                / (_minimumCellWidth + _grid.spacing.x)));
            float cellWidth = Mathf.Max(1, (width - _grid.spacing.x * (columns - 1)) / columns);
            _grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = columns;
            _grid.cellSize = new Vector2(cellWidth, cellWidth / _cellAspectRatio);
        }
    }
}
