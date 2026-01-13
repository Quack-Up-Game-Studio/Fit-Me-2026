using UnityEngine;

namespace QuackUp.Utils
{
    public enum Sign
    {
        Negative = -1,
        Zero = 0,
        Positive = 1
    }
    
    public static class InterfaceUtils
    {
        /// <summary>
        /// Instantiate a prefab that implements an interface and return the instantiated object as that interface type.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parameters"></param>
        /// <param name="gameObject"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T InstantiateAsInterface<T>(this T prefab, InstantiateParameters parameters, out GameObject gameObject) where T : class
        {
            if (prefab is not MonoBehaviour monoBehaviour)
            {
                DebugUtils.LogError($"Prefab of type {typeof(T)} is not a MonoBehaviour. Cannot instantiate.");
                gameObject = null;
                return null;
            }
            var clone = Object.Instantiate(monoBehaviour, parameters);
            gameObject = clone.gameObject;
            return clone as T;
        }
        
        public static GameObject GetGameObject<T>(this T obj) where T : class
        {
            if (obj is MonoBehaviour monoBehaviour) return monoBehaviour.gameObject;
            DebugUtils.LogError($"Object of type {typeof(T)} is not a MonoBehaviour. Cannot get GameObject.");
            return null;
        }

        public static void SetParent<T1, T2>(this T1 child, T2 parent) 
            where T1 : class
            where T2 : class
        {
            if (child is not MonoBehaviour childMonoBehaviour)
            {
                DebugUtils.LogError($"Child of type {typeof(T1)} is not a MonoBehaviour. Cannot set parent.");
                return;
            }
            if (parent is not MonoBehaviour parentMonoBehaviour)
            {
                DebugUtils.LogError($"Parent of type {typeof(T2)} is not a MonoBehaviour. Cannot set parent.");
                return;
            }
            childMonoBehaviour.transform.SetParent(parentMonoBehaviour.transform);
        }
    }
}