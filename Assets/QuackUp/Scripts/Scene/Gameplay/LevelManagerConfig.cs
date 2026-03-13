using FitMe.GameData;
using FitMe.Grid;
using FMODUnity;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Scene
{
    [CreateAssetMenu(fileName = "LevelManagerConfig", menuName = "FitMe/Scene/Gameplay/LevelManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class LevelManagerConfig : SerializedScriptableObject
    {
        [field: Title("Score Setting")]
        [field: SerializeField] public int ScorePerPlacement { get; private set; } = 100;
        [field: SerializeField] public int ScorePerChain { get; private set; } = 100;
        [field: SerializeField] public int ScorePerFitMe { get; private set; } = 1000;
        [field: SerializeField] public int OriginalLevelsPerCycle { get; private set; } = 24;
        [field: SerializeField] public GridPreset OriginalLevel { get; private set; }
        [field: SerializeField] public AnimationCurve OriginalLevelCurve { get; private set; }
        [field: SerializeField] public int ShapeLevelsPerCycle { get; private set; } = 24;
        [field: SerializeField] public LevelDatabase ShapeLevelDatabase { get; private set; }
        public GridPreset[] ShapeLevel => ShapeLevelDatabase.LevelPresets.ToArray();
        [field: SerializeField] public AnimationCurve ShapeLevelCurve { get; private set; }
        
        [field: Title("Other Setting")]
        [field: SerializeField] public bool HasCountOff { get; private set; } = true;
        [field: SerializeField] public float CountOffDuration { get; private set; } = 3f;
        [field: SerializeField] public EventReference GameplayBgm { get; private set; }
        [field: SerializeField] public ShakeSettings CameraFitShakeSettings { get; private set; }
        [field: SerializeField] public float CameraFitShakeStrengthFactor { get; private set; } = 1f;
        
        [field: Title("Scene Key")]
        [field: SerializeField] public string GameOverPanelId { get; private set; } = "GameOver";
        [field: SerializeField] public string GameplayPanelId { get; private set; } = "Gameplay";
        [field: SerializeField] public string ResultPanelId { get; private set; } = "Result";
    }
}