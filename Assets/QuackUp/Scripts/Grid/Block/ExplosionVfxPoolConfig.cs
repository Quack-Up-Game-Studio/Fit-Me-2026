using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "ExplosionVfxPoolConfig", menuName = "FitMe/Grid/Block/Explosion VFX Pool Config")]
    public sealed class ExplosionVfxPoolConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int capacityPerPrefab = 10;
        [SerializeField, Min(0)] private int prewarmCountPerPrefab = 10;

        public int CapacityPerPrefab => Mathf.Max(1, capacityPerPrefab);
        public int PrewarmCountPerPrefab => Mathf.Clamp(prewarmCountPerPrefab, 0, CapacityPerPrefab);

        private void OnValidate()
        {
            capacityPerPrefab = Mathf.Max(1, capacityPerPrefab);
            prewarmCountPerPrefab = Mathf.Clamp(prewarmCountPerPrefab, 0, capacityPerPrefab);
        }
    }
}
