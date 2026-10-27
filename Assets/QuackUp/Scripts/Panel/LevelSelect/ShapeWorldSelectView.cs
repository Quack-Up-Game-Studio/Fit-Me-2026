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
    public sealed class ShapeWorldSelectView : PanelView
    {
        public const string PanelId = "ShapeWorldSelect";
        [SerializeField, Required] private ScrollRect worldListScrollRect;
        [SerializeField, Required] private ShapeWorldSelectItemView worldItemPrefab;
        [SerializeField, Required] private Button returnToModeSelectButton;
        [SerializeField] private TMP_Text emptyStateText;
        [SerializeField, Tooltip("Locked UI-only worlds. These entries do not affect progression or saves.")]
        private List<ShapeWorldSelectComingSoonEntry> comingSoonWorlds = new();
        private ShapeWorldSelectViewModel _viewModel;
        private IDisposable _bindings;
        public override void Construct(IPanelViewModel viewModel) { base.Construct(viewModel); _viewModel = (ShapeWorldSelectViewModel)viewModel; Bind(); }
        public override void Dispose() { base.Dispose(); _bindings?.Dispose(); }
        private void Bind()
        {
            if (!returnToModeSelectButton) { DebugUtils.LogError("World select requires return button reference."); return; }
            var builder = Disposable.CreateBuilder();
            returnToModeSelectButton.OnClickAsObservable().Subscribe(_ => _viewModel.ReturnToModeSelectCommand.Execute(Unit.Default)).AddTo(ref builder);
            _viewModel.ItemInitialized.Subscribe(BuildItems).AddTo(ref builder);
            _bindings = builder.Build();
        }
        private void BuildItems(IReadOnlyList<ShapeWorldSelectItem> items)
        {
            if (!worldListScrollRect || !worldListScrollRect.content || !worldItemPrefab)
            { if (emptyStateText) { emptyStateText.text = "World selection references are not assigned."; emptyStateText.gameObject.SetActive(true); } return; }
            foreach (Transform child in worldListScrollRect.content) Destroy(child.gameObject);
            var displayItems = ShapeWorldSelectDisplayItems.Build(items, comingSoonWorlds);
            foreach (var item in displayItems)
            {
                var view = Instantiate(worldItemPrefab, worldListScrollRect.content);
                view.Construct(item, item.IsUnlocked ? _viewModel.SelectWorldCommand.Execute : null);
            }
            if (emptyStateText) { emptyStateText.text = "No worlds are configured."; emptyStateText.gameObject.SetActive(displayItems.Count == 0); }
        }
    }
}
