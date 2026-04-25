using System;
using System.Collections.Generic;
using FitMe.Tutorial;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [Serializable]
    public struct TutorialStateInfo
    {
        public string Name;
        public TutorialState State;
    }

    [Serializable]
    public class TutorialManagerDebugData : DebugDataBase
    {
        [ShowInInspector] private TutorialManager manager;
        [ShowInInspector] private TutorialStateMachine stateMachine;
        
        public TutorialManagerDebugData(TutorialManager manager, TutorialStateMachine stateMachine)
        {
            this.manager = manager;
            this.stateMachine = stateMachine;
        }
    }
    
    [Serializable]
    public class TutorialManagerInstaller : DebugableInstaller<TutorialManagerDebugData>
    {
        [OdinSerialize] private List<TutorialStateInfo> states;
        [SerializeField] private bool overrideStart;
        [SerializeField, ShowIf(nameof(overrideStart))] private string startKey;
        
        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TutorialManager>().AsSelf();
            builder.RegisterInstance(overrideStart ? startKey : string.Empty)
                .AsSelf()
                .Keyed(TutorialManager.OverrideStartKey);
            builder.Register<TutorialStateMachine>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                var stateMachine = x.Resolve<TutorialStateMachine>();
                foreach (var state in states)
                {
                    x.Inject(state.State);
                    stateMachine.AddState(state.Name, state.State);
                }
                var manager = x.Resolve<TutorialManager>();
                DebugData = new TutorialManagerDebugData(manager, stateMachine);
            });
        }
    }
}