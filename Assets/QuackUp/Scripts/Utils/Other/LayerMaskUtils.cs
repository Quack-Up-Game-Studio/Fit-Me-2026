using UnityEngine;

namespace QuackUp.Utils
{
    public static class LayerMaskUtils
    {
        /// <summary>
        /// Check if a layer is in the layer mask
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        public static bool IsInLayerMask(this LayerMask layerMask, int layer)
        {
            return (layerMask & (1 << layer)) != 0;
        }
    }
}