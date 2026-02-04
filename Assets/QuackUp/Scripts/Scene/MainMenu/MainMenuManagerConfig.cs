using FMODUnity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Scene.MainMenu
{
    [CreateAssetMenu(fileName = "MainMenuManagerConfig", menuName = "FitMe/MainMenu/MainMenuManagerConfig")]
    public class MainMenuManagerConfig : SerializedScriptableObject
    {
        [field: SerializeField] public EventReference MainMenuBgm { get; private set; }
    }
}