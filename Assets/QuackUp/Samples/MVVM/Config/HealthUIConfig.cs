using UnityEngine;

namespace QuackUp.Samples
{
    [CreateAssetMenu(fileName = "HealthUIConfig", menuName = "QuackUp/Samples/MVVM/HealthUIConfig")]
    public class HealthUIConfig : ScriptableObject
    {
        [field: SerializeField] public Vector2 HealthRange { get; private set; } = new(0f, 100f);
        [field: SerializeField] public float HealthChangeStep { get; private set; } = 10f;
    }
}