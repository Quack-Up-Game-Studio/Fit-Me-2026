using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Panel;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Scene
{
    public class SplashScreenManager : IDisposable
    {
        private readonly LoadSceneManager _loadSceneManager;
        private readonly SplashScreenMessageHub _messageHub;
        private readonly AdsService _adsService;
        private readonly CancellationTokenSource _cts = new();
        private IDisposable _bindings;

        [Inject]
        public SplashScreenManager(
            LoadSceneManager loadSceneManager,
            SplashScreenMessageHub messageHub,
            AdsService adsService)
        {
            _loadSceneManager = loadSceneManager;
            _messageHub = messageHub;
            _adsService = adsService;
            Bind();
            HideBannerInitially();
        }

        private void Bind()
        {
            var builder = Disposable.CreateBuilder();

            _messageHub.Subscribe<SplashScreenFinishedEvent>(_ => OnSplashSequenceFinished())
                .AddTo(ref builder);

            _bindings = builder.Build();
        }

        private void HideBannerInitially()
        {
            DebugUtils.Log("SplashScreenManager: HideBannerInitially invoked.");
            UniTask.Void(async (cancellationToken) =>
            {
                try
                {
                    DebugUtils.Log("SplashScreenManager: Starting loop waiting for BannerAdInstance in AdsService...");
                    BannerAdInstance bannerAdInstance = null;
                    while (bannerAdInstance == null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (_adsService.TryGetAdsInstance<BannerAdInstance>(out var instance))
                        {
                            bannerAdInstance = instance;
                            DebugUtils.Log("SplashScreenManager: BannerAdInstance successfully resolved!");
                            break;
                        }
                        await UniTask.Yield(cancellationToken);
                    }
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    DebugUtils.Log("SplashScreenManager: Calling TryHide() on BannerAdInstance.");
                    bannerAdInstance.TryHide();
                }
                catch (OperationCanceledException)
                {
                    DebugUtils.Log("SplashScreenManager: HideBannerInitially loop cancelled.");
                }
            }, _cts.Token);
        }

        private void OnSplashSequenceFinished()
        {
            DebugUtils.Log("SplashScreenManager: Received SplashScreenFinishedEvent. Switching to Main Menu scene.");
            // Switch to Main Menu scene
            _loadSceneManager.LoadScene(SceneType.MainMenu, UnityEngine.SceneManagement.LoadSceneMode.Single, false).Forget();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
