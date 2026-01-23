using System;
using UnityEngine;

namespace FitMe.Entity
{
    [Serializable]
    public class PlayerFactory : EntityFactory
    {
        [SerializeField] private PlayerEntityPreset playerEntityPreset;
        
        public override Entity Current { get; protected set; }
        public override Entity Create()
        { 
            Current = new PlayerEntity(playerEntityPreset);
            return Current;
        }
    }
}