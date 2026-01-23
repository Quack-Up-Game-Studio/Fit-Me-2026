using System;
using QuackUp.Utils;

namespace FitMe.Entity
{
    [Serializable]
    public abstract class ComponentFactory : IFactory<Component>
    {
        public abstract Component Current { get; protected set; }
        public abstract Component Create();
    }
}