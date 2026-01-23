using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Entity
{
    [Serializable]
    public abstract class EntityFactory : IFactory<Entity>
    {
        public abstract Entity Current { get; protected set; }
        public abstract Entity Create();
    }
    
    public interface IEntityUIFactory
    {
        void Create(Entity entity);
    }
}