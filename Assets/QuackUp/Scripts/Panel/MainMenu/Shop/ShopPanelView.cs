using System;
using QuackUp.IAP;
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
        
        [Inject]
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
            _bindings?.Dispose();
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