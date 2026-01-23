using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Entity
{
    [Serializable]
    public class PlayerHealthUIFactory : IEntityUIFactory
    {
        [SerializeField] private PlayerHealthUIView view;
        
        public PlayerHealthViewModel Current { get; private set; }
        public void Create(Entity entity)
        {
            if (!entity.TryGetComponent(out HealthComponent healthComponent))
            {
                throw new InvalidOperationException("Player entity does not have HealthComponent.");
            }
            Current = new PlayerHealthViewModel(healthComponent);
            view.Construct(Current);
        }
    }
}