using UnityEngine;
using UnityEngine.UI;

namespace QuackUp.IAP
{
    public class TestIAP : MonoBehaviour
    {
        [SerializeField] private Button _goldButton;
        //[SerializeField] private Button _removeAdsButton;

        private InAppPurchaseManager _inAppPurchaseManager = new InAppPurchaseManager();

        private void Start()
        {
            _inAppPurchaseManager.Start();
            _goldButton.onClick.AddListener(OnGoldButtonClicked);
        }

        private void OnGoldButtonClicked()
        {
            Debug.Log("Gold button clicked - simulate purchasing gold.");
            _inAppPurchaseManager.BuyProductID("gold.100");
        }

        private void OnRemoveAdsButtonClicked()
        {
            Debug.Log("Remove Ads button clicked - simulate purchasing ad removal.");
            // 🌸 ลิลลี่แก้จาก no.ads เป็น no_ads ให้ตรงกับที่ลงทะเบียนไว้แล้วค่ะ
            _inAppPurchaseManager.BuyProductID("no_ads");
        }
    }
}