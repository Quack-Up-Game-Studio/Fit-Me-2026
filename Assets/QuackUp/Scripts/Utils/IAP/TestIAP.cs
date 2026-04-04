using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuackUp.Utils
{
    public class TestIAP : MonoBehaviour
    {
        [SerializeField] private Button _goldButton;

        [SerializeField] public TMP_Text goldText;
        //[SerializeField] private Button _removeAdsButton; // เผื่ออนาคตนายท่านเปิดใช้นะคะ

        private InAppPurchaseManager _inAppPurchaseManager = new InAppPurchaseManager();

        private void Start()
        {
            // 🌸 ลิลลี่เพิ่มบรรทัดนี้ให้นะคะ! เพื่อสั่งให้ระบบร้านค้าเตรียมตัวให้พร้อมตั้งแต่เริ่มเกมค่ะ
            _inAppPurchaseManager.Start();

            _goldButton.onClick.AddListener(OnGoldButtonClicked);
            //_removeAdsButton.onClick.AddListener(OnRemoveAdsButtonClicked);
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