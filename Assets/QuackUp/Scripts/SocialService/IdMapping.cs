using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.SocialService
{
    [CreateAssetMenu(fileName = "IdMapping", menuName = "QuackUp/Shared/IdMapping")]
    [ShowOdinSerializedPropertiesInInspector]
    public class IdMapping : SerializedScriptableObject
    {
        [OdinSerialize] private Dictionary<string, string> _mapping = new();
        public IReadOnlyDictionary<string, string> Mapping => _mapping;
    }
}