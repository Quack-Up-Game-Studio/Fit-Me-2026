using FitMe.Grid;
using FMODUnity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Scene
{
    [CreateAssetMenu(fileName = "LevelManagerConfig", menuName = "FitMe/Level/LevelManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class LevelManagerConfig : SerializedScriptableObject
    {
        [field: Title("Score Setting")]
        [field: SerializeField] public int ScorePerPlacement { get; private set; } = 100;
        [field: SerializeField] public int ScorePerPreInfect { get; private set; } = 50;
        [field: SerializeField] public int ScorePerCombo { get; private set; } = 100;
        [field: SerializeField] public int ScorePerBomb { get; private set; } = 200;
        [field: SerializeField] public int ScorePerFitMe { get; private set; } = 10000;
        
        [field: Title("Level Setting")]
        [field: SerializeField] public GridPreset OriginalLevel { get; private set; }
        [field: SerializeField] public GridPreset[] LevelShapeLevel { get; private set; }
        
        [field: Title("Other Setting")]
        [field: SerializeField] public bool HasCountOff { get; private set; } = true;
        [field: SerializeField] public float CountOffDuration { get; private set; } = 3f;
        [field: SerializeField] public EventReference GameplayBgm { get; private set; }
        
        [field: Title("Scene Key")]
        [field: SerializeField] public string GameOverPanelId { get; private set; } = "GameOver";
        [field: SerializeField] public string GameplayPanelId { get; private set; } = "Gameplay";
        [field: SerializeField] public string ResultPanelId { get; private set; } = "Result";
    }
}