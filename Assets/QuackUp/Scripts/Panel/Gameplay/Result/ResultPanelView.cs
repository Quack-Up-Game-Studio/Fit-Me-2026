using System;
using Cysharp.Threading.Tasks;
using FMODUnity;
using PrimeTween;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QuackUp.Utils;

namespace FitMe.Panel
{
    public class ResultPanelView : PanelView
    {
        [Title("References")]
        [SerializeField] private GameObject yourScoreBlock;
        [SerializeField] private GameObject newHighScoreBlock;
        [SerializeField] private GameObject newFitMeBlock;
        [SerializeField] private GameObject notEnoughEnergyBlock;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private UIButton3D retryButton;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text fitText;
        [SerializeField] private ParticleSystem ringLight;
        [SerializeField] private CanvasGroup[] childCanvasGroups;
        
        [Title("Audios")]
        [SerializeField] EventReference newHighScoreSfx;
        [SerializeField] EventReference newFitMeSfx;

        [Title("Tween")] 
        [SerializeField] private TweenSettings<Vector3> newHighScoreScaleTweenSettings;
        [SerializeField] private TweenSettings<Vector3> newFitMeScaleTweenSettings;

        private ResultPanelViewModel ViewModel => (ResultPanelViewModel)BaseViewModel;
        
        private Tween _newHighScoreScaleTween;
        private Tween _newFitMeScaleTween;
        private IDisposable _bindings;
        
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var  disposableBuilder = Disposable.CreateBuilder();
            
            mainMenuButton.OnClickAsObservable()
                .Subscribe(_ => OnMainMenu())
                .AddTo(ref disposableBuilder);
            
            retryButton.Button.OnClickAsObservable()
                .Subscribe(_ => OnRetry())
                .AddTo(ref disposableBuilder);
            
            ViewModel.ScoreText
                .Subscribe(text => scoreText.text = text)
                .AddTo(ref disposableBuilder);
            
            ViewModel.FitText
                .Subscribe(text => fitText.text = text)
                .AddTo(ref disposableBuilder);
            
            ViewModel.DisplayResultCommand
                .SubscribeAwait((data, _) => OnDisplayResult(data), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            ViewModel.CurrentEnergy
                .Subscribe(_ => OnEnergyUpdated())
                .AddTo(ref disposableBuilder);
            ViewModel.InfiniteEnergy
                .Subscribe(_ => OnEnergyUpdated())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void OnEnergyUpdated()
        {
            var shouldEnable = ViewModel.HasEnoughEnergy(1);
            retryButton.Button.interactable = shouldEnable;
            retryButton.ApplyTint(shouldEnable ? ButtonSelectionState.Normal : ButtonSelectionState.Disabled);
            notEnoughEnergyBlock.SetActive(!shouldEnable);
        }

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            childCanvasGroups.ForEach(x =>
            {
                var active = state is VisibilityState.Visible;
                x.interactable = active;
                x.blocksRaycasts = active;
            });
            if (state != VisibilityState.Visible) return;
            newHighScoreBlock.SetActive(false);
            newFitMeBlock.SetActive(false);
            scoreText.gameObject.SetActive(false);
            fitText.gameObject.SetActive(false);
        }

        private async UniTask OnDisplayResult(DisplayResultCommandData data)
        {
            var cancellationToken = data.Promise.CancellationToken;
            cancellationToken.Register(() =>
            {
                _newHighScoreScaleTween.Complete();
                _newFitMeScaleTween.Complete();
                yourScoreBlock.SetActive(!data.IsNewHighScore);
                newHighScoreBlock.SetActive(data.IsNewHighScore);
                newFitMeBlock.SetActive(data.IsNewFitMe);
                scoreText.gameObject.SetActive(true);
                fitText.gameObject.SetActive(true);
            });
            yourScoreBlock.SetActive(!data.IsNewHighScore);
            await UniTask.WaitForSeconds(1f, cancellationToken: cancellationToken);
            await ShowHighScore(data);
            await UniTask.WaitForSeconds(1f, cancellationToken: cancellationToken);
            await ShowFitMeScore(data);
            data.Promise.TrySetResult(Unit.Default);
        }
        
        private async UniTask ShowHighScore(DisplayResultCommandData data)
        {
            var cancellationToken = data.Promise.CancellationToken;
            yourScoreBlock.SetActive(!data.IsNewHighScore);
            newHighScoreBlock.SetActive(data.IsNewHighScore);
            scoreText.gameObject.SetActive(true);
            if (!data.IsNewHighScore) return;
            ViewModel.AudioManager.PlayAudioOneShot(newHighScoreSfx, transform.position);
            _newHighScoreScaleTween = Tween.Scale(newHighScoreBlock.transform, newHighScoreScaleTweenSettings);
            await _newHighScoreScaleTween.ToUniTask(cancellationToken: cancellationToken);
            ringLight.Play();
        }
    
        private async UniTask ShowFitMeScore(DisplayResultCommandData data)
        {
            var cancellationToken = data.Promise.CancellationToken;
            newFitMeBlock.SetActive(data.IsNewFitMe);
            fitText.gameObject.SetActive(true);
            if (!data.IsNewFitMe) return;
            ViewModel.AudioManager.PlayAudioOneShot(newFitMeSfx, transform.position);
            _newFitMeScaleTween = Tween.Scale(newFitMeBlock.transform, newFitMeScaleTweenSettings);
            await _newFitMeScaleTween.ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnMainMenu()
        {
            ViewModel.ToMainMenuCommand.Execute(Unit.Default);
        }
        
        private void OnRetry()
        {
            ViewModel.ToRetryCommand.Execute(Unit.Default);
        }
    }
}
