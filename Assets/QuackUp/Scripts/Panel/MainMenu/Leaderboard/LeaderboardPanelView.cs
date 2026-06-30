using System;
using FitMe.Shared;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class LeaderboardPanelView : PanelView
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private RectTransform buttonLayoutGroup;
        [SerializeField] private Vector2 adsEnableButtonPosition;
        [SerializeField] private Vector2 adsDisableButtonPosition;
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
            ViewModel.Status
                .Subscribe(OnStatusChanged)
                .AddTo(ref disposableBuilder);
            ViewModel.AdsEnabled
                .Subscribe(enable =>
                {
                    buttonLayoutGroup.anchoredPosition = enable ? adsEnableButtonPosition : adsDisableButtonPosition;
                })
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
        
        private void OnStatusChanged(LeaderboardStatus status)
        {
            statusText.gameObject.SetActive(status != LeaderboardStatus.Loaded);
            switch (status)
            {
                case LeaderboardStatus.Loading:
                    statusText.text = "Loading...";
                    break;
                case LeaderboardStatus.Loaded:
                    statusText.text = string.Empty;
                    break;
                case LeaderboardStatus.Error:
                    statusText.text = "Error loading leaderboard. Please try again later.";
                    break;
                case LeaderboardStatus.Cancelled:
                    statusText.text = "Loading cancelled.";
                    break;
            }
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