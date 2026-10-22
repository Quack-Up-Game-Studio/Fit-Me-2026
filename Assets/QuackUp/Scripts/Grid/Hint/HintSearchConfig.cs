using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "HintSearchConfig", menuName = "FitMe/Grid/Hint/HintSearchConfig")]
    public sealed class HintSearchConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int maxSearchNodes = 50000;
        public int MaxSearchNodes => Mathf.Max(1, maxSearchNodes);
    }
}
