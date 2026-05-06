using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class PopUpTextInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private PopUpTextView popUpTextView;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(popUpTextView);
            builder.Register<PopUpTextViewModel>(Lifetime.Scoped);
        }
    }
}