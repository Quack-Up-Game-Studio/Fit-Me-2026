using System;
using FitMe.Shared;
using QuackUp.GPGS;
using QuackUp.SocialService;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.SocialService.Android
{
    [Serializable]
    public class GPGSLeaderboardHandlerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private IdMapping leaderboardIdMapping;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register(x =>
            {
                var leaderboard = x.Resolve<GPGSLeaderboard>();
                return new GPGSLeaderboardHandler(leaderboard, leaderboardIdMapping);
            }, Lifetime.Singleton).As<ILeaderboardService>();
        }
    }
}