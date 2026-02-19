using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace FitMe.Panel
{
    [Serializable]
    public class ButtonSoundHandlerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            var handlers = Object.FindObjectsByType<ButtonSoundHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            builder.RegisterBuildCallback(x =>
            {
                foreach (var handler in handlers)
                {
                    x.Inject(handler);
                }
            });
        }
    }
}