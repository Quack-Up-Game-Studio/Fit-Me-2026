using System.Collections.Generic;
using FMODUnity;
using QuackUp.Utils;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.Core
{
    [CreateAssetMenu(fileName = "LoadSceneManagerConfig", menuName = "QuackUp/Core/LoadSceneManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class LoadSceneManagerConfig : SerializedScriptableObject
    {
        [Title("Scenes"),
         HideLabel,
         ShowInInspector] private InspectorPlaceholder _sceneTitle;
        [field: OdinSerialize] public Dictionary<SceneType, SceneReference> SceneReferences { get; private set; }

        [Title("Transition"),
         HideLabel,
         ShowInInspector] private InspectorPlaceholder _transitionTitle;
        [field: SerializeField] public bool MinimumLoadingScreenDuration { get; private set; } = true;
        [field: ShowIf(nameof(MinimumLoadingScreenDuration)),
                SerializeField] public float LoadingScreenDuration { get; private set; } = 1f;
        [field: SerializeField] public EventReference TransitionSfx { get; private set; }
    }
}