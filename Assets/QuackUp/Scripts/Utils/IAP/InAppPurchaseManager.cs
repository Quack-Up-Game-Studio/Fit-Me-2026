using System;
using UnityEngine;
using UnityEngine.Purchasing;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace QuackUp.Utils
{
    public class InAppPurchaseManager : IStartable, IStoreListener
    {
        private IStoreController m_StoreController;
        private IExtensionProvider m_StoreExtensionProvider;

        public void Start()
        {
            InitializePurchasing();
        }
        
        public void InitializePurchasing() 
        {
            if (IsInitialized()) return;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct("gold_100", ProductType.Consumable,
                new StoreSpecificIds()
                {
                    {"gold_100", GooglePlay.Name}
                });
            builder.AddProduct("no_ads", ProductType.NonConsumable,
                new StoreSpecificIds()
                {
                    {"no_ads", GooglePlay.Name}
                }); 

            UnityPurchasing.Initialize(this, builder);
        }

        private bool IsInitialized() => m_StoreController != null && m_StoreExtensionProvider != null;
        
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("Yuirin have to connect store successfully.");
            m_StoreController = controller;
            m_StoreExtensionProvider = extensions;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.Log("Connect store failed: " + error);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message = null)
        {
            Debug.Log("Connect store failed: " + error + " - " + message);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (string.Equals(args.purchasedProduct.definition.id, "gold_100", StringComparison.Ordinal))
            {
                Debug.Log("Have 100 gold now! Enjoy shopping, master!");
                Object.FindAnyObjectByType<TestIAP>().goldText.text = "Gold: 100";
            }
            else if (string.Equals(args.purchasedProduct.definition.id, "no_ads", StringComparison.Ordinal))
            {
                Debug.Log("No more ads! Enjoy shopping, master!");
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogWarning($"Purchase failed: {product.definition.id}, Reason: {failureReason}");
        }
        
        public void BuyProductID(string productId)
        {
            if (IsInitialized())
            {
                m_StoreController.InitiatePurchase(productId);
            }
            else
            {
                Debug.Log("Shop is not ready. Please try again later.");
            }
        }
    }
}
