using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace FitMe.Grid
{
    #region Enums
    public enum BlockState
    {
        Normal,
        PreInfected,
        Infected,
        Protected,
        Exploding
    }

    public enum BlockInteractionState
    {
        PlacedOnSpawn,
        PickUp,
        PlacedOnGrid
    }

    public enum BlockShape
    {
        OneByOne,
        OneByTwo,
        OneByThree,
        TwoByTwo,
        Z,
        S,
        J,
        L,
        T
    }
    
    public enum BlockColor
    {
        Red,
        Yellow,
        Green,
        Purple,
        Blue
    }
    #endregion

    [Serializable]
    public class BlockModel
    {
        private readonly BlockConfig _config;
        private readonly AtomFactory _atomFactory;
        
        [Inject]
        public BlockModel(
            BlockConfig config,
            AtomFactory atomFactory)
        {
            _config = config;
            _atomFactory = atomFactory;
        }
        
        #region Inspectors
        [field: Title("Block Debug")]
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <remarks>
        /// Use <see cref="ChangeType"/> to change the block type.
        /// </remarks>
        public ReadOnlyReactiveProperty<BlockColor> BlockType => _blockType.ToReadOnlyReactiveProperty();
        public BlockConfig Config => _config;
        public BlockShape BlockShape { get; private set; }
        public List<AtomModel> Atoms { get; private set; } = new(); 
        public BlockPreset BlockPreset { get; private set; }
        public BlockState BlockState { get; set; } = BlockState.Normal;
        public ReactiveProperty<BlockInteractionState> BlockInteractionState { get; private set; } = new(Grid.BlockInteractionState.PlacedOnSpawn);
        public List<CellModel> BlockCells { get; set; }
        public int SpawnIndex { get; set; }
        public int RotationalIndex { get; set; }
        public IBlockView BlockView { get; internal set; }
        public IBlockController BlockController { get; internal set; }
        
        public ReactiveCommand<int> SetSortingLayerCommand { get; private set; } = new();
        public ReactiveCommand<int> SetSortingOrderCommand { get; private set; } = new();
        
        public Subject<Unit> UpdateGridRequested { get; } = new();
        
        private ReactiveProperty<BlockColor> _blockType = new();
        private int _originalSortingOrder;
        #endregion

        #region Schema
        public void GenerateAtom(BlockShape blockShape, BlockPreset preset)
        {
            var row = preset.BlockSize.y;
            var column = preset.BlockSize.x;
            BlockShape = blockShape;
            BlockPreset = preset;
            for (var x = 0; x < row; x++)
            {
                for (var y = 0; y < column; y++)
                {
                    if (preset.BlockSchema.schema[x, y] == 0)
                    {
                        continue;
                    }
                    var spawnPosX = -column / 2f + 0.5f + y; //0
                    var spawnPosY = row / 2f - 0.5f - x;
                    var spawnPosition = new Vector3(spawnPosX, spawnPosY, 0);
                    //DebugUtils.Log("Spawning atom at: " + spawnPosition);
                    var atom = _atomFactory.Create(spawnPosition, Quaternion.identity, out _, new InstantiateParameters
                    {
                        worldSpace = false,
                        parent = BlockView.GetGameObject().transform
                    });
                    //atom.AtomView?.SetParent(BlockView);
                    atom.ParentBlockModel.Value = this;
                    var hasTop = HasElement(x - 1, y);
                    var hasBottom = HasElement(x + 1, y);
                    var hasLeft = HasElement(x, y - 1);
                    var hasRight = HasElement(x, y + 1);
                    var settings = new SpriteOutlineSettings
                    {
                        OutlineTop = !hasTop,
                        OutlineBottom = !hasBottom,
                        OutlineLeft = !hasLeft,
                        OutlineRight = !hasRight
                    };
                    atom.AtomView.SetOutline(settings);
                    Atoms.Add(atom);
                }
            }
            // var spritePositionX = -column / 2f + 0.5f;
            // var spritePositionY = row / 2f - 0.5f;
            // spriteRenderer.transform.localPosition = new Vector3(spritePositionX, spritePositionY, 0);
            // if (_config.UseAtomSprite && BlockView)
            // {
            //     BlockView.gameObject.SetActive(false);
            // }
            // else
            // {
            //     Atoms.ForEach(a => a.SpriteRenderer.enabled = false);
            // }
            BlockPreset.GenerateSchema();
            return;

            bool HasElement(int x, int y)
            {
                if (x < 0 || x >= row || y < 0 || y >= column)
                    return false;
                return preset.BlockSchema.schema[x, y] == 1;
            }
        }
        #endregion
        
        #region Utils
        public void ChangeType(BlockColor color, bool updateGrid = true)
        {
            _blockType.Value = color;
            if (!updateGrid) return;
            UpdateGridRequested.OnNext(Unit.Default);
        }
        #endregion
    }
}