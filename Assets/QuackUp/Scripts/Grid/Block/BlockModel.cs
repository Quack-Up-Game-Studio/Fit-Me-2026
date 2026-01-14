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

    public enum BlockInteractionType
    {
        None,
        PickUp
    }
    
    // public enum FlashState
    // {
    //     None,
    //     Flashing,
    //     PreInfectFlash
    // }
    
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
            AtomFactory atomFactory)
        {
            _config = config;
            _atomFactory = atomFactory;
            Initialize();
        }
        
        #region Inspectors
        [field: Title("Block Debug")]
        [field: SerializeField, DisplayAsString] public BlockTypes BlockType { get; private set; }
        [field: SerializeField, DisplayAsString] public string BlockFace { get; private set; }
        [field: SerializeField, ReadOnly] public List<AtomModel> Atoms { get; private set; } = new();
        [field: SerializeField, ReadOnly] public BlockPreset BlockPreset { get; private set; }
        // [SerializeField, DisplayAsString] private FlashState flashState;
        [field: SerializeField, DisplayAsString] public BlockState BlockState { get; private set; } = BlockState.Normal;
        [field: SerializeField, DisplayAsString] public BlockInteractionType BlockInteractionType { get; private set; } = BlockInteractionType.None;
        [field: SerializeField, DisplayAsString] public bool IsPlaced { get; set; }
        [field: SerializeField, ReadOnly] public List<CellModel> BlockCells { get; set; }
        //[field: SerializeField, ReadOnly] public BlockView BlockView { get; private set; }
        public int SpawnIndex { get; set; }
        //public BlockState beforeExplodeState = BlockState.Normal;
        #endregion

        #region Initialization
        private void Initialize()
        {
            Atoms.ForEach(a => a.ParentBlockModel = this);
        }

        // private void StartInfectTimer()
        // {
        //     _infectionSubscription?.Dispose();
        //     if (BlockState is not BlockState.Infected) return;
        //     _infectionSubscription = Observable
        //         .Interval(TimeSpan.FromSeconds(GridManager.Instance.RandomInfectedTime))
        //         .Subscribe(_ => GridManager.Instance.InfectAdjacentBlocks(this));
        // }
        #endregion

        #region Events
        void OnDestroy()
        {
            
        }
        #endregion

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

        /*#region Infection

        /// <summary>
        /// Change state of the block to Protected state.
        /// In a Protected state, the block cannot be PreInfect and Infect.
        /// </summary>
        public async UniTask Protected()
        {
            BlockState = BlockState.Protected;
            
            await UniTask.WaitForSeconds(_protectedTime,
                cancellationToken: destroyCancellationToken);
            BlockState = BlockState.Normal;
        }
        
        public async UniTask PreInfect()
        {
            var infectionConfig = GridManager.Instance.CurrentGridPreset.InfectionSettings;
            BlockState = BlockState.PreInfected;
            if (BlockView) BlockView.PreInfect();
            StartFlashing(FlashState.PreInfectFlash);
            beforeExplodeState = BlockState.PreInfected;
            AudioManager.Instance.PlayAudioOneShot(preInfectSfx, transform.position);
            await UniTask.WaitForSeconds(infectionConfig.PreInfectTime,
                cancellationToken: destroyCancellationToken);
            if (BlockState is BlockState.Exploding) return;
            GridManager.Instance.InfectBlock(this);
        }
        
        public void Infect()
        {
            BlockState = BlockState.Infected;
            beforeExplodeState = BlockState.Infected;
            if (BlockView) BlockView.Infect();
            StopFlashing();
            StartInfectTimer();
            AudioManager.Instance.PlayAudioOneShot(infectSfx, transform.position);
        }
        
        public void Disinfect()
        {
            SetColor(originalAtomColor);
            BlockState = BlockState.Normal;
            _infectionSubscription?.Dispose();
        }
        #endregion*/
        
        
        
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
                if (BlockView)
                {
                    Destroy(BlockView.gameObject);
                }
                BlockView = Instantiate(blockViewPrefab, transform.position, Quaternion.identity, transform);
                BlockView.SetType(type);
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
        
        /*public void StartFlashing(FlashState flashState)
        {
            switch (flashState)
            {
                case FlashState.Flashing:
                    if(_flashTween.isAlive) return;
                    
                    if (_preInfectTween.isAlive)
                    { _preInfectTween.Complete(); }
                    SetColor(originalAtomColor);
                    _flashTween = Tween.Custom(originalAtomColor, Color.red, flashDuration, cycles: -1, cycleMode: CycleMode.Yoyo,
                        onValueChange: SetColor);
                    break;
                
                case FlashState.PreInfectFlash:
                    if (BlockState != BlockState.PreInfected) return;
                    if(_preInfectTween.isAlive) return;
                    SetColor(originalAtomColor);
                    _preInfectTween = Tween.Custom(originalAtomColor, _infectColor, flashDuration, cycles: -1, cycleMode: CycleMode.Yoyo,
                        onValueChange: SetColor);
                    break;
                
                case FlashState.None:
                    if (BlockState is BlockState.PreInfected)
                        StartFlashing(FlashState.PreInfectFlash);
                    else if (BlockState is BlockState.Infected or BlockState.Normal)
                        StopFlashing();
                    break;
            }
        }
        
        public void StopFlashing()
        {
            if (_flashTween.isAlive)
            {
                _flashTween.Complete();
                _flashTween = default;
                SetColor(originalAtomColor);
            }

            switch (BlockState)
            {
                case BlockState.Infected or BlockState.PreInfected:
                    StopPreInfectFlash();
                    break;
                
                case BlockState.Normal:
                    flashState = FlashState.None;
                    break;
            }
        }
        
        public void StopPreInfectFlash()
        {
            if (_preInfectTween.isAlive)
            {
                _preInfectTween.Complete();
                _preInfectTween = default;
            }
            
            switch (BlockState)
            {
                case BlockState.PreInfected:
                    StartFlashing(FlashState.PreInfectFlash);
                    break;
                
                case BlockState.Infected:
                    flashState = FlashState.None;
                    SetColor(_infectColor);
                    break;
            }
        }
        
        public void StopAllFlash()
        {
            if (_flashTween.isAlive)
            { _flashTween.Stop(); }
            
            if (_preInfectTween.isAlive)
            { _preInfectTween.Stop(); }
            
            SetColor(originalAtomColor);
        }*/
        #endregion
    }
}