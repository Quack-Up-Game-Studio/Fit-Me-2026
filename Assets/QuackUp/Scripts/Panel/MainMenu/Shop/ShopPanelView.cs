using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public struct ButtonInfo
    {
        public ProductId ProductId;
        public Button Button;
        public TMP_Text PriceText;
    }
    
    public class ShopPanelView : PanelView
    {
        [SerializeField] private Button closeButton;
        
        [Title("Consume Item Buttons")]
        [SerializeField] private ButtonInfo[] _consumableItemButton;
        
        [Title("Subscription Buttons")]
        [SerializeField] private ButtonInfo[] _subscriptionButton;
        
        [SerializeField] private string mainMenuPanelId = "MainMenu";
        private ShopPanelViewModel ViewModel => (ShopPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        private CancellationTokenSource _priceCts;
        
        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
            ViewModel.RegisterOnIAPReady(StartPriceUpdateLoop);
            ViewModel.RegisterOnPurchaseSuccess(UpdatePrices);
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            closeButton.OnClickAsObservable()
                .Subscribe(_ => OnCloseButtonClicked())
                .AddTo(ref disposableBuilder);
            foreach (var button in _consumableItemButton)
            {
                var productId = button.ProductId.ToProductString();
                if (button.PriceText != null)
                    button.PriceText.text = ViewModel.GetPrice(productId);
                button.Button
                    .OnClickAsObservable()
                    .Subscribe(_ => OnBuyButtonClicked(productId))
                    .AddTo(ref disposableBuilder);
            }
            foreach (var button in _subscriptionButton)
            {
                var productId = button.ProductId.ToProductString();
                if (button.PriceText != null)
                    button.PriceText.text = ViewModel.GetPrice(productId);
                button.Button
                    .OnClickAsObservable()
                    .Subscribe(_ => OnBuyButtonClicked(productId))
                    .AddTo(ref disposableBuilder);
            }
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_priceCts != null)
            {
                _priceCts.Cancel();
                _priceCts.Dispose();
                _priceCts = null;
            }
            
            ViewModel.UnregisterOnPurchaseSuccess(UpdatePrices);
            ViewModel.UnregisterOnIAPReady(StartPriceUpdateLoop);
        }

        private void StartPriceUpdateLoop()
        {
            UpdatePrices();
            _priceCts = new CancellationTokenSource();
            PriceUpdateLoop(_priceCts.Token).Forget();
        }
        
        private async UniTaskVoid PriceUpdateLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromMinutes(1), cancellationToken: token);
                UpdatePrices();
            }
        }
        
        public void UpdatePrices()
        {
            foreach (var button in _consumableItemButton)
            {
                if (button.PriceText != null)
                    button.PriceText.text = ViewModel.GetPrice(button.ProductId.ToProductString());
            }

            bool isFreeTrial = ViewModel.IsInFreeTrial();
            bool hasVip = ViewModel.HasActiveSubscription();
            
            foreach (var button in _subscriptionButton)
            {
                if (button.PriceText != null)
                {
                    if (isFreeTrial)
                        button.PriceText.text = "Free Trial Active";
                    else if (hasVip)
                        button.PriceText.text = "Already have VIP";
                    else
                        button.PriceText.text = ViewModel.GetPrice(button.ProductId.ToProductString());
                }

                button.Button.interactable = !hasVip;
            }
        }
        
        private void OnBuyButtonClicked(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogWarning("ShopPanelView: ProductId is empty! Check button setup in Inspector.");
                return;
            }
            ViewModel.OnBuyButtonClicked(productId);
        }

        private void OnCloseButtonClicked()
        {
            if (!TryGetCrossfadeRule(mainMenuPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(mainMenuPanelId, rule.crossfadeSettings));
        }
    }
}