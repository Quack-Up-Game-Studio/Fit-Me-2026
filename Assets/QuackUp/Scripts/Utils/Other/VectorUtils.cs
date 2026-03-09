using UnityEngine;

namespace QuackUp.Utils
{
    public static class VectorUtils
    {
        /// <summary>
        /// Returns a random float between the x and y components of the given Vector2.
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public static float RandomWithinRange(this Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }
        
        /// <summary>
        /// Returns a random int between the x and y components of the given Vector2Int.
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public static int RandomWithinRange(this Vector2Int range)
        {
            return Random.Range(range.x, range.y);
        }
        
        /// <summary>
        /// Swaps the x and y components of a Vector2Int.
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2Int Swap(this Vector2Int vector)
        {
            return new Vector2Int(vector.y, vector.x);
        }
        
        /// <summary>
        /// Swaps the x and y components of a Vector2.
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Swap(this Vector2 vector)
        {
            return new Vector2(vector.y, vector.x);
        }
    }
}