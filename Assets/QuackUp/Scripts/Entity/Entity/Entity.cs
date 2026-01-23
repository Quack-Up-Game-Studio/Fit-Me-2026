using System;
using System.Collections.Generic;
using Redcode.Extensions;

namespace FitMe.Entity
{
    public interface IEntity
    {
        
    }
    public abstract class Entity : IEntity
    {
        private readonly List<Component> _components = new();
        public EntityPreset Preset { get; private set; }
        
        public Entity(EntityPreset preset)
        {
            Preset = preset;
        }
        
        public void AddComponent(Component component)
        {
            if (component is null)
                throw new ArgumentNullException(nameof(component));
            if (_components.Contains(component))
                throw new ArgumentException("Component already added to entity", nameof(component));
            _components.Add(component);
        }

        public void RemoveComponent(Component component)
        {
            if (component is null)
                throw new ArgumentNullException(nameof(component));
            if (!_components.Contains(component))
                throw new ArgumentException("Component not found in entity", nameof(component));
            _components.Remove(component);
        }

        public bool TryGetComponent<T>(out T component) where T : Component
        {
            foreach (var comp in _components)
            {
                if (comp is not T typedComponent) continue;
                component = typedComponent;
                return true;
            }
            component = null;
            return false;
        }
        
        public bool TryGetComponents<T>(out List<T> components) where T : Component
        {
            components = new List<T>();
            foreach (var comp in _components)
            {
                if (comp is not T typedComponent) continue;
                components.Add(typedComponent);
            }
            return components.Count > 0;
        }
    }
}