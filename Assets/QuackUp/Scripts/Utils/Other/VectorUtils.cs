using UnityEngine;

namespace QuackUp.Utils
{
    public static class VectorUtils
    {
        public static float RandomBetweenRange(this Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }
        
        public static int RandomBetweenRange(this Vector2Int range)
        {
            return Random.Range(range.x, range.y);
        }
    }
}