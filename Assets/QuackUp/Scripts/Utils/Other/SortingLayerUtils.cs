using System;
using UnityEngine;

namespace QuackUp.Utils
{
    public static class SortingLayerUtils
    {
        public static bool TryGetNextSortingLayer(int sortingLayerId, out int nextSortingLayerId)
        {
            nextSortingLayerId = -1;
            int layerIndex = Array.FindIndex(SortingLayer.layers, layer => layer.id == sortingLayerId);
            if (layerIndex == -1) return false;
            var finalLayerIndex = layerIndex + 1;
            if (finalLayerIndex >= SortingLayer.layers.Length) return false;
            nextSortingLayerId = SortingLayer.layers[finalLayerIndex].id;
            return true;
        }
    }
}