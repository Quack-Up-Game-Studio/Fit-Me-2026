using System;
using FitMe.Shared;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class LeaderboardPanelView : PanelView
    {
        [SerializeField] private Button closeButton;
        // [SerializeField] private Button classicTabButton;
        // [SerializeField] private Button levelShapeTabButton;
        
        private LeaderboardPanelViewModel ViewModel => (LeaderboardPanelViewModel)BaseViewModel;
        
        private IDisposable _bindings;
        
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            closeButton.OnClickAsObservable()
                .Subscribe(_ => OnCloseButtonClicked())
                .AddTo(ref disposableBuilder);
            // classicTabButton.OnClickAsObservable()
            //     .Subscribe(_ => ChangeGameModeTab(GameMode.Classic))
            //     .AddTo(ref disposableBuilder);
            // levelShapeTabButton.OnClickAsObservable()
            //     .Subscribe(_ => ChangeGameModeTab(GameMode.LevelShape))
            //     .AddTo(ref disposableBuilder);
            // ViewModel.CurrentGameMode
            //     .Subscribe(OnGameModeChanged)
            //     .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnCloseButtonClicked()
        {
            if (!TryGetCrossfadeRule("MainMenu", out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData("MainMenu", rule.crossfadeSettings));
        }

        // private void OnGameModeChanged(GameMode gameMode)
        // {
        //     classicTabButton.interactable = gameMode == GameMode.LevelShape;
        //     levelShapeTabButton.interactable = gameMode == GameMode.Classic;
        // }
        
        // private void ChangeGameModeTab(GameMode gameMode)
        // {
        //     ViewModel.ChangeTabCommand.Execute(gameMode);
        // }
    }
}