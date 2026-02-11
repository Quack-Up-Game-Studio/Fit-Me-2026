using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Scene
{
    public class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image lockIcon;
        [SerializeField] private GameObject[] stars;
        [SerializeField] private TMP_Text levelText;
        
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(LevelButtonViewModel vm)
        {
            levelText.text = vm.LevelID.ToString();

            vm.IsLocked.Subscribe(isLocked => {
                lockIcon.gameObject.SetActive(isLocked);
                button.interactable = !isLocked;
            }).AddTo(this);

            button.OnClickAsObservable()
                .Subscribe(_ => vm.OnClickCommand.Execute(Unit.Default))
                .AddTo(this);
            
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();

            _bindings = disposableBuilder.Build();
        }
        
        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}
