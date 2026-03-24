using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel.Tutorial
{
    [Serializable]
    public class TextTutorialInstaller : IInstaller
    {
        [SerializeField] private TextTutorialView view;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(view);
            builder.Register<TextTutorialViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                view.gameObject.SetActive(true);
                x.Resolve<TextTutorialViewModel>();
            });
        }
    }
}