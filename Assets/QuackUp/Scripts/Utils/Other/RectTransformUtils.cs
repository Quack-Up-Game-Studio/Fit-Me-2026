using UnityEngine;

namespace QuackUp.Utils
{
    public static class RectTransformUtils
    {
        public static void ClampTo(this RectTransform rectTransform, RectTransform clampTo, RectTransformInset margin = default)
        {
            // Calculate bounds
            var canvasBounds = new Bounds(clampTo.rect.center, clampTo.rect.size);
            var tooltipBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                clampTo, rectTransform);

            // Calculate overflow
            var overflow = new Vector2(
                Mathf.Min(0, canvasBounds.max.x - margin.right - tooltipBounds.max.x) 
                + Mathf.Max(0, canvasBounds.min.x + margin.left - tooltipBounds.min.x),
                Mathf.Min(0, canvasBounds.max.y - margin.top - tooltipBounds.max.y) 
                + Mathf.Max(0, canvasBounds.min.y + margin.bottom - tooltipBounds.min.y)
            );

            // Apply correction
            rectTransform.localPosition += (Vector3)overflow;
        }
    }
}