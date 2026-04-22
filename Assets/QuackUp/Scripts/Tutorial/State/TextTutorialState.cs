using System;
using Cysharp.Threading.Tasks;
using FitMe.Panel;
using FitMe.Panel.Tutorial;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class TextTutorialState : TutorialState
    {
        [SerializeField, TextArea] private string text;
        [FormerlySerializedAs("usePreviousSize")] [SerializeField] private bool usePreviousPanelSize;
        [SerializeField, HideIf(nameof(usePreviousPanelSize))] private RectTransformInset panelInset;
        [SerializeField] private bool usePreviousCharacterSize;
        [SerializeField, HideIf(nameof(usePreviousCharacterSize))] private Vector2 characterSize;
        [SerializeField] private bool usePreviousCharacterPosition;
        [SerializeField, HideIf(nameof(usePreviousCharacterPosition))] private Vector3 characterPosition;
        [SerializeField] private bool usePreviousCharacterRotation;
        [SerializeField, HideIf(nameof(usePreviousCharacterRotation))] private Vector3 characterRotation;
        [SerializeField] private bool hasCarveWindow;
        [SerializeField, ShowIf(nameof(hasCarveWindow))] private bool usePreviousCarveWindowInset;
        [SerializeField, HideIf("@!hasCarveWindow || usePreviousCarveWindowInset")] private RectTransformInset carveWindowInset;
        [SerializeField] private Sprite image;
        [SerializeField] private bool hideWhenExit;
        [SerializeField] private bool hasNextButton = true;
        [SerializeField] private bool blockInput = true;

        #region Debug
        [SerializeField] private RectTransform panelRectTransform;
        [SerializeField] private RectTransform characterRectTransform;
        [SerializeField] private RectTransform carveWindowRectTransform;

        [Button(nameof(CopyPanelTransformData))]
        [ShowIf(nameof(panelRectTransform))]
        private void CopyPanelTransformData()
        {
            panelInset = RectTransformInset.FromRectTransform(panelRectTransform);
        }
        [Button(nameof(CopyCharacterTransformData))]
        [ShowIf(nameof(characterRectTransform))]
        private void CopyCharacterTransformData()
        {
            characterSize = characterRectTransform.sizeDelta;
            characterPosition = characterRectTransform.localPosition;
            characterRotation = characterRectTransform.localEulerAngles;
        }
        [Button(nameof(CopyCarveWindowTransformData))]
        [ShowIf(nameof(carveWindowRectTransform))]
        private void CopyCarveWindowTransformData()
        {
            carveWindowInset = RectTransformInset.FromRectTransform(carveWindowRectTransform);
        }
        #endregion
        
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
            ViewModel.SetData(new TextTutorialData
            {
                Text = text,
                Image = image,
                PanelInset = usePreviousPanelSize ? null : panelInset,
                CharacterSize = usePreviousCharacterSize ? null : characterSize,
                CharacterPosition = usePreviousCharacterPosition ? null : characterPosition,
                CharacterRotation = usePreviousCharacterRotation ? null : characterRotation,
                HasCarveWindow = hasCarveWindow,
                CarveWindowInset = usePreviousCarveWindowInset ? null : carveWindowInset,
                HasNextButton = hasNextButton
            });
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