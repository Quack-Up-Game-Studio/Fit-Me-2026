using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuackUp.Utils
{
    [RequireComponent(typeof(Button))]
    public class UIButton3D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Title("References")] 
        [SerializeField] private GameObject up;
        [SerializeField] private GameObject down;
        
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (!_button)
            {
                Debug.LogError("UIButton3D requires a Button component to function properly.");
                return;
            }
            up.SetActive(true);
            down.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            OnClick().Forget();
        }

        private async UniTaskVoid OnClick()
        {
            up.SetActive(false);
            down.SetActive(true);
            await UniTask.WaitForSeconds(0.05f, cancellationToken: destroyCancellationToken);
            up.SetActive(true);
            down.SetActive(false);
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            up.SetActive(true);
            down.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            up.SetActive(false);
            down.SetActive(true);
        }
    }
}