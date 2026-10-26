using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "ExplosionVfxPoolConfig", menuName = "FitMe/Grid/Block/VFX Pool Config")]
    public sealed class ExplosionVfxPoolConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int capacityPerPrefab = 10;
        [SerializeField, Min(0)] private int prewarmCountPerPrefab = 10;
        [SerializeField, Min(0)] private int placementPrewarmCountPerPrefab = 2;

        public int CapacityPerPrefab => Mathf.Max(1, capacityPerPrefab);
        public int PrewarmCountPerPrefab => Mathf.Clamp(prewarmCountPerPrefab, 0, CapacityPerPrefab);
        public int PlacementPrewarmCountPerPrefab => Mathf.Clamp(placementPrewarmCountPerPrefab, 0, CapacityPerPrefab);

        private void OnValidate()
        {
            capacityPerPrefab = Mathf.Max(1, capacityPerPrefab);
            prewarmCountPerPrefab = Mathf.Clamp(prewarmCountPerPrefab, 0, capacityPerPrefab);
            placementPrewarmCountPerPrefab = Mathf.Clamp(placementPrewarmCountPerPrefab, 0, capacityPerPrefab);
        }
    }
}
