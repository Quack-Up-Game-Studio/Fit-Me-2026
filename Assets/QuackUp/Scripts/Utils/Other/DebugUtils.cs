using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace QuackUp.Utils
{
    public static class DebugUtils
    {
        [Conditional("ENABLE_DEBUG")]
        public static void Log(object message)
        {
            Debug.Log(message);
        }
        
        [Conditional("ENABLE_DEBUG")]
        public static void LogWarning(object message)
        {
            Debug.LogWarning(message);
        }
        
        [Conditional("ENABLE_DEBUG")]
        public static void LogError(object message)
        {
            Debug.LogError(message);
        }
    }
}