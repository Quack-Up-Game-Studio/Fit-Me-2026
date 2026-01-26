using System.Collections.Generic;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Entity
{
    public class EntityManager
    {
        private readonly EntityManagerConfig _config;
        private readonly List<IEntityLifetimeScope> _sceneEntity;
        private readonly List<Entity> _entities = new();
        
        public IReadOnlyList<Entity> Entities => _entities;
        
        [Inject]
        public EntityManager(
            EntityManagerConfig config,
            List<IEntityLifetimeScope> sceneEntity)
        {
            _config = config;
            _sceneEntity = sceneEntity;
        }
        
        public void CreateSceneEntities()
        {
            foreach (var entityScope in _sceneEntity)
            {
                var entity = entityScope.CreateEntity();
                _entities.Add(entity);
            }
        }

        public bool TryCreateEntity<T>(EntityType type, out T entity, out GameObject lifetimeScope,
            InstantiateParameters? instantiateParameters = null) where T : Entity
        {
            entity = null;
            lifetimeScope = null;
            if (!_config.EntityDict.TryGetValue(type, out var prefab)) return false;
            instantiateParameters ??= new InstantiateParameters();
            var instance = prefab.InstantiateAsInterface(instantiateParameters.Value, out lifetimeScope);
            entity = instance.CreateEntity() as T;
            _entities.Add(entity);
            return true;
        }
        
        public bool TryGetEntityOfType<T>(out T entity) where T : Entity
        {
            foreach (var e in _entities)
            {
                if (e is not T typedEntity) continue;
                entity = typedEntity;
                return true;
            }
            entity = null;
            return false;
        }
        
        public bool TryGetEntitiesOfType<T>(out List<T> entities) where T : Entity
        {
            entities = new List<T>();
            foreach (var e in _entities)
            {
                if (e is not T typedEntity) continue;
                entities.Add(typedEntity);
            }
            return entities.Count > 0;
        }
        
        public bool TryGetEntityOfEntityType<T>(EntityType type, out T entity) where T : Entity
        {
            foreach (var e in _entities)
            {
                if (e.Preset.EntityType != type) continue;
                if (e is not T typedEntity) continue;
                entity = typedEntity;
                return true;
            }
            entity = null;
            return false;
        }
        
        public bool TryGetEntitiesOfEntityType<T>(EntityType type, out List<T> entities) where T : Entity
        {
            entities = new List<T>();
            foreach (var e in _entities)
            {
                if (e.Preset.EntityType != type) continue;
                if (e is not T typedEntity) continue;
                entities.Add(typedEntity);
            }
            return entities.Count > 0;
        }
    }
}