using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    [AddComponentMenu("Layout/Shape Level Select Layout Group")]
    public sealed class ShapeLevelSelectLayoutGroup : LayoutGroup
    {
        private const int Columns = 4;
        [SerializeField] private Vector2 cellSize = new(240, 240);
        [SerializeField] private Vector2 spacing = new(50, 50);

        private float AvailableWidth => Mathf.Max(0, rectTransform.rect.width - padding.horizontal);
        private float GridWidth => cellSize.x * Columns + spacing.x * (Columns - 1);
        private float ItemScale => Mathf.Min(1, AvailableWidth / Mathf.Max(1, GridWidth));
        private int RowCount => (rectChildren.Count + Columns - 1) / Columns;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal, padding.horizontal + GridWidth, -1, 0);
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
            var start = padding.left + (AvailableWidth - GridWidth * scale) * 0.5f;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                m_Tracker.Add(this, child, DrivenTransformProperties.ScaleX | DrivenTransformProperties.ScaleY);
                child.localScale = new Vector3(scale, scale, 1);
                SetChildAlongAxisWithScale(child, 0,
                    start + i % Columns * (cellSize.x + spacing.x) * scale, cellSize.x, scale);
            }
        }

        public override void SetLayoutVertical()
        {
            var scale = ItemScale;
            for (var i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxisWithScale(rectChildren[i], 1,
                    padding.top + i / Columns * (cellSize.y + spacing.y) * scale, cellSize.y, scale);
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
