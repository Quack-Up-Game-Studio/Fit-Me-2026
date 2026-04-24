using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    [Serializable]
    public class BackGroundEffectInstaller : IInstaller
    {
        [SerializeField] private BackGroundEffectView _backGroundEffectView;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(_backGroundEffectView);
        }
    }
}
