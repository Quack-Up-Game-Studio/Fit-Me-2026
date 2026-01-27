using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene.UI.Score
{
    [Serializable]
    public class ScoreUiInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private ScoreView scoreView;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(scoreView);
            builder.Register<ScoreViewModel>(Lifetime.Scoped);
        }
    }
}
