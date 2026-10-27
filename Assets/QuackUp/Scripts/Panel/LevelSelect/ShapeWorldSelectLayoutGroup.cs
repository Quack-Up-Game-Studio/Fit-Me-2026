using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    [AddComponentMenu("Layout/Shape World Select Layout Group")]
    public sealed class ShapeWorldSelectLayoutGroup : LayoutGroup
    {
        private const int Columns = 2;
        [SerializeField] private Vector2 cellSize = new(400, 400);
        [SerializeField] private Vector2 spacing = new(50, 50);

        private float AvailableWidth => Mathf.Max(0, rectTransform.rect.width - padding.horizontal);
        private float ItemScale => AvailableWidth / Mathf.Max(1, cellSize.x * Columns + spacing.x);
        private int RowCount => (rectChildren.Count + Columns - 1) / Columns;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal,
                padding.horizontal + cellSize.x * Columns + spacing.x, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            var height = padding.vertical +
                (RowCount * cellSize.y + Mathf.Max(0, RowCount - 1) * spacing.y) * ItemScale;
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            var scale = ItemScale;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                var rowStart = i / Columns * Columns;
                var itemsInRow = Mathf.Min(Columns, rectChildren.Count - rowStart);
                var rowWidth = (itemsInRow * cellSize.x + (itemsInRow - 1) * spacing.x) * scale;
                var start = padding.left + (AvailableWidth - rowWidth) * 0.5f;
                var x = start + i % Columns * (cellSize.x + spacing.x) * scale;
                // The supplied prefab has fixed-size artwork children. Scale the
                // whole item, not just its root rect, so art and hit targets fit.
                m_Tracker.Add(this, child, DrivenTransformProperties.ScaleX | DrivenTransformProperties.ScaleY);
                child.localScale = new Vector3(scale, scale, 1);
                SetChildAlongAxisWithScale(child, 0, x, cellSize.x, scale);
            }
        }

        public override void SetLayoutVertical()
        {
            var scale = ItemScale;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var y = padding.top + i / Columns * (cellSize.y + spacing.y) * scale;
                SetChildAlongAxisWithScale(rectChildren[i], 1, y, cellSize.y, scale);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            cellSize = new Vector2(Mathf.Max(1, cellSize.x), Mathf.Max(1, cellSize.y));
            spacing = new Vector2(Mathf.Max(0, spacing.x), Mathf.Max(0, spacing.y));
            base.OnValidate();
        }
#endif
    }
}
