using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Entity
{
    public enum EntityType
    {
        Player,
    }
    [CreateAssetMenu(fileName = "EntityManagerConfig", menuName = "FitMe/Entity/EntityManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class EntityManagerConfig : SerializedScriptableObject
    {
        [field: OdinSerialize] private Dictionary<EntityType, IEntityLifetimeScope> entityDict = new();
        public IReadOnlyDictionary<EntityType, IEntityLifetimeScope> EntityDict => entityDict;
    }
}