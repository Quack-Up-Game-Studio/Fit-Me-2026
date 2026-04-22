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
             
            // test products
            builder.AddProduct("gold.100", ProductType.Consumable,
                new StoreSpecificIds()
                {
                    {"gold.100", GooglePlay.Name}
                });
            
            // Consumable products
            builder.AddProduct("2_1energy", ProductType.Consumable,
                new StoreSpecificIds()
                {
                    {"2_1energy", GooglePlay.Name}
                });
            builder.AddProduct("3_2energy", ProductType.Consumable,
                new StoreSpecificIds()
                {
                    {"3_2energy", GooglePlay.Name}
                });
            builder.AddProduct("max_energy", ProductType.Consumable,
                new StoreSpecificIds()
                {
                    {"max_energy", GooglePlay.Name}
                });
            
            // Subscription products
            builder.AddProduct("monthlypass", ProductType.Subscription,
                new StoreSpecificIds()
                {
                    {"monthlypass", GooglePlay.Name}
                }); 
            builder.AddProduct("quarterltypass", ProductType.Subscription,
                new StoreSpecificIds()
                {
                    {"quarterltypass", GooglePlay.Name}
                }); 
            builder.AddProduct("annuallypass", ProductType.Subscription,
                new StoreSpecificIds()
                {
                    {"annuallypass", GooglePlay.Name}
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
            switch (args.purchasedProduct.definition.id)
            {
                //test products
                case "gold.100":
                    Debug.Log("Have 100 gold now! Enjoy shopping, master!");
                    break;
                
                // Consumable products
                case "2_1energy":
                    Debug.Log("Have 2 energy now! Enjoy shopping, master!");
                    break;
                case "3_2energy":
                    Debug.Log("Have 3 energy now! Enjoy shopping, master!");
                    break;
                case "max_energy":
                    Debug.Log("Have max energy now! Enjoy shopping, master!");
                    break;
                
                // Subscription products
                case "monthlypass":
                    Debug.Log("Monthly pass activated! Enjoy shopping, master!");
                    break;
                case "quarterltypass":
                    Debug.Log("Quarterly pass activated! Enjoy shopping, master!");
                    break;
                case "annuallypass":
                    Debug.Log("Annual pass activated! Enjoy shopping, master!");
                    break;
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
