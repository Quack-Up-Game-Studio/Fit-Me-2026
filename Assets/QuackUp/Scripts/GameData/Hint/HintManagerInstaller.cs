using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.GameData
{
    [Serializable]
    public sealed class HintManagerDebugData : DebugDataBase
    {
        [ShowInInspector] private HintManager manager;

        public HintManagerDebugData(HintManager manager)
        {
            this.manager = manager;
        }
    }

    [Serializable]
    public sealed class HintManagerInstaller : DebugableInstaller<HintManagerDebugData>
    {
        [ShowInInspector] private InspectorPlaceholder title;
        [SerializeField] private HintManagerConfig config;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config);
            builder.RegisterEntryPoint<HintManager>().AsSelf();
            builder.RegisterBuildCallback(container =>
            {
                var manager = container.Resolve<HintManager>();
                DebugData = new HintManagerDebugData(manager);
            });
        }
    }
}
