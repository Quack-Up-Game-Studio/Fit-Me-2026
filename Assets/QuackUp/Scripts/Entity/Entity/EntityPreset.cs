using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Entity
{
    public interface IEntityPreset
    {
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    public abstract class EntityPreset : SerializedScriptableObject, IEntityPreset
    {
        [field: SerializeField] public EntityType EntityType { get; private set; }
        [field: SerializeField] public string Name { get; private set; }
    }
}