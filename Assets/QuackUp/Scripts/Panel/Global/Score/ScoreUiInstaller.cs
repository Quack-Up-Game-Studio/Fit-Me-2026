using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class ScoreUiInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private ScoreView scoreView;
        [SerializeField] private Transform scoreTransform;
        [SerializeField] private Transform fitmeTransform;
        
        [SerializeField] private PopUpScoreView popUpScoreViewPrefab;
        [SerializeField] private Transform popUpScoreParent;
        
        public void Install(IContainerBuilder builder)
        {
            // References
            builder.RegisterInstance(scoreTransform).Keyed(ScoreView.ScoreTransformKey);
            builder.RegisterInstance(fitmeTransform).Keyed(ScoreView.FitmeTransformKey);
            
            // Score
            builder.RegisterComponent(scoreView);
            builder.Register<ScoreViewModel>(Lifetime.Scoped);
            
            // PopUp Score
            builder.RegisterInstance(popUpScoreViewPrefab);
            builder.RegisterInstance(popUpScoreParent).Keyed(PopUpScoreFactory.PopUpScoreParent);
            builder.Register<PopUpScoreViewModel>(Lifetime.Scoped);
            builder.Register<PopUpScoreFactory>(Lifetime.Scoped);
        }
    }
}
