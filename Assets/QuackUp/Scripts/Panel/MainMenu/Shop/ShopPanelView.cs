using System;
using Debug = QuackUp.Utils.DebugUtils;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Redcode.Extensions;
using QuackUp.Utils;

namespace FitMe.Panel
{
    public struct ButtonInfo
    {
        public ProductId ProductId;
        public CustomTintButton CustomTintButton;
        public TMP_Text PriceText;
    }
    
    public class ShopPanelView : PanelView
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button subManagerButton;
        [SerializeField] private GameObject uiGroup;
        [SerializeField] private CanvasGroup[] childCanvasGroups;
        
        [Title("Consume Item Buttons")]
        [SerializeField] private ButtonInfo[] _consumableItemButton;
        
        [Title("Subscription Buttons")]
        [SerializeField] private ButtonInfo[] _subscriptionButton;
        
        [Title("Popup Settings")]
        [SerializeField] private GameObject lostConnectionPopup;
        
        [SerializeField] private string mainMenuPanelId = "MainMenu";
        private ShopPanelViewModel ViewModel => (ShopPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        private IDisposable _priceUpdateTimer;
        
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
            ViewModel.OnIAPReady
                .Subscribe(_ =>
                {
                    if (lostConnectionPopup)
                        lostConnectionPopup.SetActive(false);

                    UpdatePrices();
                    StartPriceUpdateTimer();
                })
                .AddTo(ref disposableBuilder);
            ViewModel.OnPurchaseSuccess
                .Subscribe(_ => UpdatePrices())
                .AddTo(ref disposableBuilder);
            subManagerButton.OnClickAsObservable()
                .Subscribe(_ => OnSubscriptionManager())
                .AddTo(ref disposableBuilder);
            foreach (var button in _consumableItemButton)
            {
                var productId = button.ProductId.ToProductString();
                if (button.PriceText)
                    button.PriceText.text = ViewModel.GetPrice(productId);
                button.CustomTintButton.Button
                    .OnClickAsObservable()
                    .Subscribe(_ => OnBuyButtonClicked(productId))
                    .AddTo(ref disposableBuilder);
            }
            foreach (var button in _subscriptionButton)
            {
                var productId = button.ProductId.ToProductString();
                if (button.PriceText)
                    button.PriceText.text = ViewModel.GetPrice(productId);
                button.CustomTintButton.Button
                    .OnClickAsObservable()
                    .Subscribe(_ => OnBuyButtonClicked(productId))
                    .AddTo(ref disposableBuilder);
            }
            _bindings = disposableBuilder.Build();
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
            if (state == VisibilityState.Hidden) return;
            
            if (ViewModel.IsIAPReady)
            {
                UpdatePrices();
            }
            else
            {
                ViewModel.ReinitializeCommand.Execute(Unit.Default);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            _priceUpdateTimer?.Dispose();
        }

        private void StartPriceUpdateTimer()
        {
            _priceUpdateTimer?.Dispose();
            _priceUpdateTimer = Observable.Interval(TimeSpan.FromMinutes(1))
                .Subscribe(_ => UpdatePrices());
        }
        
        private void UpdatePrices()
        {
            OnOfflineMode();
            

            bool isFreeTrial = ViewModel.IsInFreeTrial();
            bool hasVip = ViewModel.HasActiveSubscription();
            
            foreach (var button in _consumableItemButton)
            {
                if (button.PriceText)
                {
                    button.PriceText.text = hasVip ? "Already have VIP" : ViewModel.GetPrice(button.ProductId.ToProductString());
                }
                var interactable = !hasVip;
                button.CustomTintButton.Button.interactable = interactable;
                button.CustomTintButton.ApplyTint(interactable ? ButtonSelectionState.Normal : ButtonSelectionState.Disabled);
            }
            
            foreach (var button in _subscriptionButton)
            {
                if (button.PriceText)
                {
                    if (isFreeTrial)
                        button.PriceText.text = "Free Trial Active";
                    else if (hasVip)
                        button.PriceText.text = "Already have VIP";
                    else
                        button.PriceText.text = ViewModel.GetPrice(button.ProductId.ToProductString());
                }

                var interactable = !hasVip;
                button.CustomTintButton.Button.interactable = interactable;
                button.CustomTintButton.ApplyTint(interactable ? ButtonSelectionState.Normal : ButtonSelectionState.Disabled);
            }
        }
        
        private void OnOfflineMode()
        {
            if (!ViewModel.IsIAPReady)
            {
                if (lostConnectionPopup)
                    lostConnectionPopup.SetActive(true);
                uiGroup.SetActive(false);
                ViewModel.ReinitializeCommand.Execute(Unit.Default);
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

        private void OnSubscriptionManager()
        {
            Application.OpenURL($"https://play.google.com/store/account/subscriptions?package={Application.identifier}");
        }
    }
}