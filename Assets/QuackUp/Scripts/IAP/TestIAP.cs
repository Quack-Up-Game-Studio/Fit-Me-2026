using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace QuackUp.IAP
{
    public class TestIAP : MonoBehaviour
    {
        [SerializeField] private Button _goldButton;
        [SerializeField] private Button _removeAdsButton;

        private InAppPurchaseManager _inAppPurchaseManager;

        [Inject]
        public void Construct(InAppPurchaseManager inAppPurchaseManager)
        {
            _inAppPurchaseManager = inAppPurchaseManager;
        }

        private void Start()
        {
            if (_inAppPurchaseManager == null)
            {
                Debug.LogError("InAppPurchaseManager not injected in TestIAP.");
                return;
            }
            
            _goldButton.onClick.AddListener(OnGoldButtonClicked);
            if (_removeAdsButton != null)
                _removeAdsButton.onClick.AddListener(OnRemoveAdsButtonClicked);
        }

        private void OnGoldButtonClicked()
        {
            Debug.Log("Gold button clicked - simulate purchasing gold.");
            _inAppPurchaseManager.BuyProductID(ProductIds.GoldTest);
        }

        private void OnRemoveAdsButtonClicked()
        {
            Debug.Log("Remove Ads button clicked - simulate purchasing ad removal.");
            // 🌸 ลิลลี่แก้จาก no.ads เป็น no_ads ให้ตรงกับที่ลงทะเบียนไว้แล้วค่ะ
            // Note: Currently we don't have a specific no_ads product in ProductIds, 
            // but the subscription passes remove ads.
            _inAppPurchaseManager.BuyProductID("no_ads");
        }
    }
}