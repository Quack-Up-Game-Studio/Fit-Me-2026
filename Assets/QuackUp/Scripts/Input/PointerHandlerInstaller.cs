using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Input
{
    [Serializable]
    public class PointerHandlerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private Camera camera;
        [SerializeField] private Canvas canvas;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(camera);
            builder.RegisterComponent(canvas);
            builder.Register<IPointerHandler, PointerHandler>(Lifetime.Singleton);
        }
    }
}