using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QuackUp.Utils
{
    public class UIButton3D : CustomTintButton
    {
        [Title("References")] 
        [SerializeField] private GameObject up;
        [SerializeField] private GameObject down;

        protected override void Awake()
        {
            base.Awake();
            
            up.SetActive(true);
            down.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            up.SetActive(true);
            down.SetActive(false);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            var btn = Button;
            if (btn != null && !btn.interactable) return;
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

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            var btn = Button;
            if (btn != null && !btn.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            up.SetActive(false);
            down.SetActive(true);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            var btn = Button;
            if (btn != null && !btn.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            up.SetActive(true);
            down.SetActive(false);
        }
    }
}