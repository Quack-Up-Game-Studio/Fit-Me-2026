using System;
using FitMe.Shared;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class ModeSelectView : PanelView
    {
        [Title("References")]
        [SerializeField] private Button normalModeButton;
        [SerializeField] private Button levelShapeModeButton;
        
        private ModeSelectViewModel _vm;

        private IDisposable _bindings;
        
        [Inject]
        public void Construct(ModeSelectViewModel vm)
        {
            _vm = vm;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            normalModeButton.OnClickAsObservable()
                .Subscribe(_ => OnOriginalMode())
                .AddTo(ref disposableBuilder);
            
            levelShapeModeButton.OnClickAsObservable()
                .Subscribe(_ => OnLevelShapeMode())
                .AddTo(ref disposableBuilder);
            
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnOriginalMode()
        {
            _vm.SelectGameModeCommand.Execute(GameMode.Classic);
        }
        
        private void OnLevelShapeMode()
        {
            _vm.SelectGameModeCommand.Execute(GameMode.LevelShape);
        }
    }
}
