using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Scene
{
    [CreateAssetMenu(fileName = "LevelManagerConfig", menuName = "FitMe/Level/LevelManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class LevelManagerConfig : SerializedScriptableObject
    {
        [TabGroup("Settings", "Score")]
        [SerializeField] public int scorePerPlacement = 100;
        [TabGroup("Settings", "Score")]
        [SerializeField] public int scorePerPreInfect = 50;
        [TabGroup("Settings", "Score")]
        [SerializeField] public int scorePerCombo = 100;
        [TabGroup("Settings", "Score")]
        [SerializeField] public int scorePerBomb = 200;
        [TabGroup("Settings", "Score")]
        [SerializeField] public int scorePerFitMe = 10000;
    }
}