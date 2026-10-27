using System.Collections.Generic;
using FitMe.Shared;
using FMODUnity;
using PrimeTween;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "BlockManagerConfig", menuName = "FitMe/Grid/Block/BlockManagerConfig")]
    public class BlockManagerConfig : SerializedScriptableObject
    {
        [Title("Shape Tray")]
        [field: SerializeField, Min(.01f), Tooltip("Resting Shape Mode tray scale only. Small supplies shrink to avoid overlap; dense supplies may overlap.")]
        public float TrayBlockScale { get; private set; } = .5f;
        [field: SerializeField] public float TrayTiltMin { get; private set; } = -15f;
        [field: SerializeField] public float TrayTiltMax { get; private set; } = 15f;
        [field: SerializeField, Tooltip("Normalized padding inside Tray_Panel artwork, excluding rounded rim and bottom shadow.")]
        public Vector2 TrayInset { get; private set; } = new(.1f, .15f);

        [Title("Random Settings")]
        [field: SerializeField] public int MaxRandomAmount { get; private set; } = 3;
        [field: SerializeField] public int MinRandomAmount { get; private set; } = 1;
        [field: SerializeField] public bool UseSmartRandom { get; private set; } = true;
        [field: SerializeField] public int SmartRandomThreshold { get; private set; } = 9;
        [field: SerializeField] public int SmartRandomDepth { get; private set; } = 1;
        [field: SerializeField] public float PreviewScale { get; private set; } = 0.25f;
        [field: SerializeField] public float ObjectScale { get; private set; } = 0.5f;
        [field: OdinSerialize] private Dictionary<BlockShape, BlockPreset> _blockPresetDictionary = new();
        public IReadOnlyDictionary<BlockShape, BlockPreset> BlockPresetDictionary => _blockPresetDictionary;
        [field: OdinSerialize] private Dictionary<BlockShape, BlockConfig> _blockConfigDictionary = new();
        public IReadOnlyDictionary<BlockShape, BlockConfig> BlockConfigDictionary => _blockConfigDictionary;
        [field: SerializeField] public ArbitraryBlockConfig ArbitraryBlockConfig { get; private set; }
        [field: SerializeField] public ExplosionVfxPoolConfig ExplosionVfxPoolConfig { get; private set; }
        [field: SerializeField] public bool CanRefill { get; private set; } = true;
        [field: OdinSerialize] private Dictionary<BlockShape, int> _bagSettings = new();
        public IReadOnlyDictionary<BlockShape, int> BagSettings => _bagSettings;
        [field: SerializeField] public int PreviewCount;
        [field: SerializeField] public float SpawnSpace = 1f;
        
        
        [Title("Block Settings")]
        [field: SerializeField, SortingLayer] public int SpawnSortingLayer { get; private set; }
        [field: SerializeField, SortingLayer] public int PickUpSortingLayer { get; private set; }
        [field: SerializeField, SortingLayer] public int GridSortingLayer { get; private set; }
        [field: OdinSerialize] private Dictionary<BlockColor, Color> _atomColorDict = new();
        public IReadOnlyDictionary<BlockColor, Color> AtomColorDict => _atomColorDict;
        [field: SerializeField] public bool AllowPickUpAfterPlacement { get; private set; }
        [field: SerializeField] public bool RotateClockwise { get; private set; } = true;
        [field: SerializeField] public float PickUpScaleMultiplier { get; private set; } = 1.2f;
        [field: SerializeField, Min(0f), Tooltip("Base BoxCollider2D edge radius while a block is resting on its spawn/tray and can be picked up.")]
        public float AtomEdgeRadius { get; private set; } = .25f;
        [field: SerializeField] public Vector2 SwitchIdleTimeRange { get; private set; } = new(30f, 60f);
        [field: SerializeField] public float LongTapDuration { get; private set; } = 0.2f;
        [field: SerializeField] public bool UseBlockDragTween { get; private set; } = true;
        [field: SerializeField] public bool UseBlockScaleTween { get; private set; } = true;
        [field: SerializeField] public TweenSettings BlockDragTweenSettings { get; private set; }
        [field: SerializeField] public float BlockDragInertia { get; private set; } = 0.1f;
        [field: SerializeField] public bool UseDynamicDragOffset { get; private set; } = true;
        [field: SerializeField] public AnimationCurve DynamicDragOffsetCurve { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [field: SerializeField] public Vector2 DynamicOffsetX { get; private set; } = new Vector2(0f, 2.5f);
        [field: SerializeField] public Vector2 DynamicOffsetY { get; private set; } = new Vector2(0f, 5f);
        
        [Title("Audios")] 
        [field: SerializeField] public EventReference PlaceSucceedSfx { get; private set; }
        [field: SerializeField] public EventReference PlaceFailSfx { get; private set; }

        public IBlockPresentationConfig GetBlockConfig(BlockShape shape, IBlockPresentationConfig configOverride = null)
        {
            if (configOverride != null)
                return configOverride;

            if (_blockConfigDictionary.TryGetValue(shape, out var blockConfig))
                return blockConfig;

            throw new System.ArgumentException($"Block config '{shape}' not found in BlockConfigDictionary.");
        }
    }
}