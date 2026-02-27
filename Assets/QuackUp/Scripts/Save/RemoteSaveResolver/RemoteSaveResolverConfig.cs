using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.Save
{
    [CreateAssetMenu(fileName = "RemoteSaveResolverConfig", menuName = "QuackUp/Save/RemoteSaveResolverConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class RemoteSaveResolverConfig : SerializedScriptableObject
    {
        [field: SerializeField] public ConflictSolution FallbackSolution { get; private set; } = ConflictSolution.UseLocal;
        [OdinSerialize] private List<ISaveConflictSolutionProvider> _solutionProviders = new();
        public IReadOnlyList<ISaveConflictSolutionProvider> SolutionProviders => _solutionProviders;
    }
}