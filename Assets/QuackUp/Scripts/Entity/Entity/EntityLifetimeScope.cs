using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using VContainer;
using VContainer.Unity;

namespace FitMe.Entity
{
    public interface IEntityLifetimeScope
    {
        Entity CreateEntity();
    }
    [ShowOdinSerializedPropertiesInInspector]
    public abstract class EntityLifetimeScope : SerializedLifetimeScope, IEntityLifetimeScope
    {
        [OdinSerialize] private EntityFactory entityFactory;
        [OdinSerialize] private List<ComponentFactory> componentFactories;
        [OdinSerialize] private List<IEntityUIFactory> uiFactories;
        
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBuildCallback(x =>
            {
                x.Inject(entityFactory);
                componentFactories.ForEach(x.Inject);
                uiFactories.ForEach(x.Inject);
            });
        }

        public Entity CreateEntity()
        {
            var entity = entityFactory.Create();
            foreach (var componentFactory in componentFactories)
            {
                var component = componentFactory.Create();
                entity.AddComponent(component);
            }
            foreach (var uiFactory in uiFactories)
            {
                uiFactory.Create(entity);
            }
            return entity;
        }
    }
}