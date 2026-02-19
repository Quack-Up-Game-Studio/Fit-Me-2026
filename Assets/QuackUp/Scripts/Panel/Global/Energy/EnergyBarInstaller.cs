using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class EnergyBarInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private EnergyBarView energyBarView;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(energyBarView);
            builder.Register<EnergyBarViewModel>(Lifetime.Scoped);
        }
    }
}