using System;
using Cysharp.Threading.Tasks;
using FitMe.Panel;
using FitMe.Panel.Tutorial;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class TextTutorialState : TutorialState
    {
        [SerializeField, TextArea] private string text;
        [SerializeField] private bool usePreviousSize;
        [SerializeField, HideIf(nameof(usePreviousSize))] private RectTransformInset panelInset;
        [SerializeField] private Sprite image;
        [SerializeField] private bool hideWhenExit;
        [SerializeField] private bool hasNextButton = true;
        [SerializeField] private bool blockInput = true;
        
        protected TextTutorialViewModel ViewModel;
        private IDisposable _subscription;

        [Inject]
        public void SetViewModel(TextTutorialViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            if (ViewModel == null)
            {
                DebugUtils.LogError($"{GetType().Name}: ViewModel is not set.");
                return;
            }
            _subscription = ViewModel.OnNextCommand
                .Subscribe(_ => OnNext());
            ViewModel.SetData(text, image, usePreviousSize, panelInset, hasNextButton);
            if (ViewModel.VisibilityState.CurrentValue is not VisibilityState.Visible)
                await UniTask.WhenAll(ViewModel.Show(), ViewModel.ChangeInputBlockState(blockInput));
            await ViewModel.DisplayData();
        }

        public void OnNext()
        {
            StateMachine.Next().Forget();
        }
        
        public override async UniTask Exit()
        {
            await base.Exit();
            if (ViewModel == null)
            {
                DebugUtils.LogError($"{GetType().Name}: ViewModel is not set.");
                return;
            }
            _subscription.Dispose();
            if (hideWhenExit)
                await UniTask.WhenAll(ViewModel.Hide(), ViewModel.ChangeInputBlockState(false));
        }
    }
}