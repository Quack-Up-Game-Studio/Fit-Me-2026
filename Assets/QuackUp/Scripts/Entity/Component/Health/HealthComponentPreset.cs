using UnityEngine;

namespace FitMe.Entity
{
    [CreateAssetMenu(fileName = "HealthComponentPreset", menuName = "FitMe/Entity/Component/HealthComponentPreset")]
    public class HealthComponentPreset : ComponentPreset
    {
        [field: SerializeField] public Vector2 HealthRange { get; private set; } = new(0, 100);
        [field: SerializeField] public float InitialHealth { get; private set; } = 100;
    }
}