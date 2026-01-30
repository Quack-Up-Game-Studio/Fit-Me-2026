using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Scene
{
    [CreateAssetMenu(fileName = "LevelManagerConfig", menuName = "FitMe/Level/LevelManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class LevelManagerConfig : SerializedScriptableObject
    {
        [field: SerializeField] public int ScorePerPlacement { get; private set; } = 100;
        [field: SerializeField] public int ScorePerPreInfect { get; private set; } = 50;
        [field: SerializeField] public int ScorePerCombo { get; private set; } = 100;
        [field: SerializeField] public int ScorePerBomb { get; private set; } = 200;
        [field: SerializeField] public int ScorePerFitMe { get; private set; } = 10000;
        
        [field: SerializeField] public bool HasCountOff { get; private set; } = true;
        [field: SerializeField] public float CountOffDuration { get; private set; } = 3f;
    }
}