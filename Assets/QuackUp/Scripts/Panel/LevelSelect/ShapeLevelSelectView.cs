using System;
using System.Collections.Generic;
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
        [SerializeField, Required] private Button backToWorldsButton;
        [SerializeField, Required] private TMP_Text worldNameText;
        [SerializeField] private TMP_Text worldSubtitleText;
        [SerializeField] private TMP_Text emptyStateText;

        private ShapeLevelSelectViewModel _viewModel;
        private IDisposable _bindings;
        private int _originalSiblingIndex;

        private void Awake()
        {
            _originalSiblingIndex = transform.GetSiblingIndex();
        }

        protected override void OnTransitionOut(TransitionCommandData transitionCommandData)
        {
            transform.SetAsLastSibling();
            base.OnTransitionOut(transitionCommandData);
        }

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state == VisibilityState.Hidden)
                transform.SetSiblingIndex(_originalSiblingIndex);
        }

        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            _viewModel = (ShapeLevelSelectViewModel)viewModel;
            Bind();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void BuildLevelList(IReadOnlyList<ShapeLevelSelectItem> items)
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
            foreach (Transform child in content)
                Destroy(child.gameObject);
            foreach (var item in items)
            {
                var itemView = Instantiate(levelItemPrefab, content);
                itemView.Construct(item, _viewModel.SelectLevelCommand.Execute);
            }

            if (emptyStateText)
            {
                emptyStateText.gameObject.SetActive(items.Count == 0);
                emptyStateText.text = "No Shape Levels are configured.";
            }
        }

        private void Bind()
        {
            if (!backToWorldsButton)
            {
                DebugUtils.LogError("Shape Level Select requires a Back to Worlds button reference.");
                return;
            }

            var disposableBuilder = Disposable.CreateBuilder();
            if (worldNameText)
                _viewModel.WorldDisplayName.Subscribe(value => worldNameText.text = value).AddTo(ref disposableBuilder);
            else
                DebugUtils.LogError("Shape Level Select requires a World Name text reference.");

            if (worldSubtitleText)
                _viewModel.WorldSubtitle.Subscribe(value =>
                {
                    worldSubtitleText.text = value;
                    worldSubtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
                }).AddTo(ref disposableBuilder);

            backToWorldsButton.OnClickAsObservable().Subscribe(_ => _viewModel.BackToWorldsCommand.Execute(Unit.Default)).AddTo(ref disposableBuilder);
            _viewModel.ItemInitialized
                .Subscribe(BuildLevelList)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
    }
}
