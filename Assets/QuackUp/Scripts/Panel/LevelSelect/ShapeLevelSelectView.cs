using System;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public sealed class ShapeLevelSelectView : PanelView
    {
        [Title("References")]
        [SerializeField, Required] private ScrollRect levelListScrollRect;
        [SerializeField, Required] private ShapeLevelSelectItemView levelItemPrefab;
        [SerializeField, Required] private Button returnToModeSelectButton;
        [SerializeField] private TMP_Text emptyStateText;

        private ShapeLevelSelectViewModel _viewModel;
        private IDisposable _bindings;

        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            _viewModel = (ShapeLevelSelectViewModel)viewModel;
            BuildLevelList();
            Bind();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void BuildLevelList()
        {
            if (!levelListScrollRect || !levelListScrollRect.content || !levelItemPrefab)
            {
                if (emptyStateText)
                {
                    emptyStateText.gameObject.SetActive(true);
                    emptyStateText.text = "Shape Level Select references are not assigned.";
                }
                return;
            }

            var content = levelListScrollRect.content;
            foreach (var item in _viewModel.Items)
            {
                var itemView = Instantiate(levelItemPrefab, content);
                itemView.Construct(item, _viewModel.SelectLevelCommand.Execute);
            }

            if (emptyStateText)
            {
                emptyStateText.gameObject.SetActive(_viewModel.Items.Count == 0);
                emptyStateText.text = "No Shape Levels are configured.";
            }
        }

        private void Bind()
        {
            if (!returnToModeSelectButton)
            {
                DebugUtils.LogError("Shape Level Select requires a Return to Mode Select button reference.");
                return;
            }

            var disposableBuilder = Disposable.CreateBuilder();
            returnToModeSelectButton.OnClickAsObservable()
                .Subscribe(_ => _viewModel.ReturnToModeSelectCommand.Execute(Unit.Default))
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
    }
}
