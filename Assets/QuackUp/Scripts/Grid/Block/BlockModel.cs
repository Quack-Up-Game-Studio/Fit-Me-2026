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
        None,
        PickUp,
        Placed
    }
    
    public enum BlockTypes
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
            AtomFactory atomFactory,
            IBlockView blockView)
        {
            _config = config;
            _atomFactory = atomFactory;
            BlockView = blockView;
            Initialize();
        }
        
        #region Inspectors
        [field: Title("Block Debug")]
        public Guid Id { get; set; } = Guid.NewGuid();
        public BlockTypes BlockType { get; private set; }
        public string BlockFace { get; private set; }
        public List<AtomModel> Atoms { get; private set; } = new(); 
        public BlockPreset BlockPreset { get; private set; }
        public BlockState BlockState { get; private set; } = BlockState.Normal;
        public ReactiveProperty<BlockInteractionState> BlockInteractionState { get; private set; } = new(Grid.BlockInteractionState.None);
        public List<CellModel> BlockCells { get; set; }
        public int SpawnIndex { get; set; }
        public int RotationalIndex { get; set; }
        public IBlockView BlockView { get; private set; }
        public TransformData TransformData { get; set; } = new();
        
        private int _originalSortingOrder;
        #endregion
        
        public void Initialize()
        {
            Atoms.ForEach(x => x.ParentBlockModel = this);
        }

        #region Schema
        public void GenerateAtom(string blockFace, BlockPreset preset)
        {
            var row = preset.BlockSize.y;
            var column = preset.BlockSize.x;
            BlockFace = blockFace;
            BlockPreset = preset;
            for (var x = 0; x < row; x++)
            {
                for (var y = 0; y < column; y++)
                {
                    if (preset.BlockSchema.schema[x, y] == 0)
                    {
                        continue;
                    }
                    float spawnPosX = -column / 2f + 0.5f + y;
                    float spawnPosY = row / 2f - 0.5f - x;
                    Vector3 spawnPosition = new Vector3(spawnPosX, spawnPosY, 0);
                    var atom = _atomFactory.Create(spawnPosition, Quaternion.identity, out _);
                    atom.ParentBlockModel = this;
                    var hasTop = HasElement(x - 1, y);
                    var hasBottom = HasElement(x + 1, y);
                    var hasLeft = HasElement(x, y - 1);
                    var hasRight = HasElement(x, y + 1);
                    // atom.SpriteOutlineController.outlineTop = !hasTop;
                    // atom.SpriteOutlineController.outlineBottom = !hasBottom;
                    // atom.SpriteOutlineController.outlineLeft = !hasLeft;
                    // atom.SpriteOutlineController.outlineRight = !hasRight;
                    // atom.SpriteOutlineController.UpdateOutline();
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
            
            bool HasElement(int x, int y)
            {
                if (x < 0 || x >= row || y < 0 || y >= column)
                    return false;
                return preset.BlockSchema.schema[x, y] == 1;
            }
        }
        #endregion
        
        #region Utils
        public void ChangeType(BlockTypes type, bool updateGrid = true)
        {
            BlockType = type;
            if (!useAtomSprite)
            {
                if (!GetBlockView(out var blockViewPrefab))
                {
                    Debug.LogWarning($"BlockView for block type {BlockType} not found. Fall back to atom sprite.");
                    useAtomSprite = true;
                    Atoms.ForEach(atom => atom.SpriteRenderer.enabled = true);
                    ChangeType(type, updateGrid);
                    return;
                }
                if (Grid.BlockView)
                {
                    Destroy(Grid.BlockView.gameObject);
                }
                Grid.BlockView = Instantiate(blockViewPrefab, transform.position, Quaternion.identity, transform);
                Grid.BlockView.SetType(type);
            }
            else
            {
                if (!BlockManager.Instance.AtomColorDictionary.TryGetValue(type, out var color))
                {
                    Debug.LogError($"Color for block type {type} not found.");
                    return;
                }
                originalAtomColor = color;
                SetColor(color);
            }
            if (!updateGrid) return;
            GridManager.Instance.UpdateBlockOnGrid(this);
        }

        private bool GetBlockView(out BlockView blockView)
        {
            blockView = null;
            if (!BlockManager.Instance.BlockViewDictionary.TryGetValue(BlockFace, out blockView))
            {
                Debug.LogWarning($"BlockView for block type {BlockType} not found in the database.");
                return false;
            }
            if (!blockView)
            {
                Debug.LogWarning($"BlockView for block type {BlockType} and face {BlockFace} is null.");
                return false;
            }
            return true;
        }
        #endregion
    }
}