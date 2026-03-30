using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuackUp.Utils
{
    public class TestIAP : MonoBehaviour
    {
        [SerializeField] private Button _goldButton;

        [SerializeField] public TMP_Text goldText;
        //[SerializeField] private Button _removeAdsButton;
        
        private InAppPurchaseManager _inAppPurchaseManager = new InAppPurchaseManager();
        
        private void Start()
        {
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
            _inAppPurchaseManager.BuyProductID("no.ads");
        }
    }
}
