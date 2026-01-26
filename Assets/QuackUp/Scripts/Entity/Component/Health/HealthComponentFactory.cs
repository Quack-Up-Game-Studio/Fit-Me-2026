using System;
using UnityEngine;

namespace FitMe.Entity
{
    [Serializable]
    public class HealthComponentFactory : ComponentFactory
    {
        [SerializeField] private HealthComponentPreset preset;
        
        public override Component Current { get; protected set; }
        public override Component Create()
        {
            Current = new HealthComponent(preset);
            return Current;
        }
    }
}