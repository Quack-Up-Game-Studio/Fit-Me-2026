using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    [Serializable]
    public struct SplashItemSettings
    {
        [Required] public CanvasGroup canvasGroup;
        public float fadeInDuration;
        public float showDuration;
        public float fadeOutDuration;
        public Ease fadeInEase;
        public Ease fadeOutEase;
    }

    public class SplashPanelView : PanelView
    {
        [SerializeField, Required] private HoldButton skipButton;
        [SerializeField] private List<SplashItemSettings> splashItems = new();

        private SplashPanelViewModel ViewModel => (SplashPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        private CancellationTokenSource _sequenceCts;
        private CancellationTokenSource _currentItemCts;
        private Tween _currentTween;
        private float _lastSkipTime;

        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var builder = Disposable.CreateBuilder();

            // Start splash sequence when panel becomes active and interactable
            ViewModel.InputState
                .Where(state => state == InputState.Active)
                .Subscribe(_ => StartSplashSequence())
                .AddTo(ref builder);

            // Bind skip button events directly (hard requirement)
            skipButton.OnClick.AsObservable()
                .Subscribe(_ => SkipCurrentSplash())
                .AddTo(ref builder);

            _bindings = builder.Build();
        }

        private void StartSplashSequence()
        {
            DebugUtils.Log("SplashPanelView: StartSplashSequence invoked.");
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();

            PlaySplashSequence(_sequenceCts.Token).Forget();
        }

        private async UniTask PlaySplashSequence(CancellationToken cancellationToken)
        {
            try
            {
                // Ensure all canvas groups start fully hidden (hard requirement)
                foreach (var item in splashItems)
                {
                    item.canvasGroup.alpha = 0f;
                }

                for (int i = 0; i < splashItems.Count; i++)
                {
                    var item = splashItems[i];
                    DebugUtils.Log($"SplashPanelView: Playing splash item index {i}.");

                    _currentItemCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    try
                    {
                        // Fade In
                        _currentTween = Tween.Alpha(item.canvasGroup, 0f, 1f, item.fadeInDuration, item.fadeInEase);
                        await _currentTween.ToYieldInstruction().ToUniTask(cancellationToken: _currentItemCts.Token);

                        // Show / Wait
                        await UniTask.WaitForSeconds(item.showDuration, cancellationToken: _currentItemCts.Token);

                        // Fade Out
                        _currentTween = Tween.Alpha(item.canvasGroup, 1f, 0f, item.fadeOutDuration, item.fadeOutEase);
                        await _currentTween.ToYieldInstruction().ToUniTask(cancellationToken: _currentItemCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        
                        DebugUtils.Log($"SplashPanelView: Splash item index {i} skipped.");
                        
                        // Stop current active tween if it is running
                        if (_currentTween.isAlive)
                        {
                            _currentTween.Stop();
                        }
                        
                        // Current item skipped - immediately zero alpha to transition instantly
                        item.canvasGroup.alpha = 0f;
                    }
                    finally
                    {
                        _currentItemCts?.Dispose();
                        _currentItemCts = null;
                    }
                }

                DebugUtils.Log("SplashPanelView: Splash sequence completed naturally.");
                // Sequence finished, notify VM
                ViewModel.SplashFinishedCommand.Execute(Unit.Default);
            }
            catch (OperationCanceledException)
            {
                DebugUtils.Log("SplashPanelView: Splash sequence cancelled.");
            }
        }

        private void SkipCurrentSplash()
        {
            // Cooldown of 0.2 seconds between skips to prevent duplicate triggers from click + hold events
            if (Time.time - _lastSkipTime < 0.2f) return;
            _lastSkipTime = Time.time;

            if (_currentTween.isAlive)
            {
                _currentTween.Stop();
            }
            try
            {
                _currentItemCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Safe skip if CTS is already disposed
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            
            if (_currentTween.isAlive)
            {
                _currentTween.Stop();
            }

            try
            {
                _sequenceCts?.Cancel();
            }
            catch (ObjectDisposedException) {}
            _sequenceCts?.Dispose();

            try
            {
                _currentItemCts?.Cancel();
            }
            catch (ObjectDisposedException) {}
            _currentItemCts?.Dispose();

            _bindings?.Dispose();
        }
    }
}