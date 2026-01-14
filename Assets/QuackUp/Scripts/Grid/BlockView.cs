using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Grid
{
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockView : SerializedMonoBehaviour
    {
        [Serializable]
        private record SkinWrapper
        {
            [ShowInInspector, Sirenix.OdinInspector.ReadOnly] private SkeletonRenderer _skeletonRenderer;
            [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
            public BlockTypes blockType;
            [SpineSkin(dataField: nameof(_skeletonRenderer))] public string skinName;
            
            public SkinWrapper(SkeletonRenderer skeletonRenderer, BlockTypes blockType, string skinName)
            {
                _skeletonRenderer = skeletonRenderer;
                this.blockType = blockType;
                this.skinName = skinName;
            }
        }
        #region Inspectors
        [TitleGroup("References")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private SpriteRenderer infectedSpriteRenderer;
        
        [Title("Settings")]
        [SerializeField] private Color originalColor = Color.white;
        [SerializeField] private float pickUpScaleMultiplier = 1.2f;
        [SerializeField] private Vector2 switchIdleTimeRange = new(30f, 60f);
        
        [Title("Animations")]
        [SerializeField, SpineAnimation] string[] idleAnimations;
        [SerializeField, SpineAnimation] string pickUpAnimation;
        [SerializeField, SpineAnimation] string explodeAnimation;
        [SerializeField, SpineAnimation] string preInfectedAnimation;
        
        [Title("VFX")]
        [field: OdinSerialize] private Dictionary<BlockTypes, ParticleSystem> explodeVfx = new();
        
        [TitleGroup("Skins")]
        [ShowInInspector, HideLabel]
        [DetailedInfoBox("Read Me",
            "Due to a certain limitation of Spine handling of the attributes, the skin selector cannot be drawn under dictionary, " +
            "and has to be deconstructed into a list of SkinWrapper objects.\n" +
            "You can deconstruct the dictionary into a list by clicking the 'Deconstruct' button, " +
            "and then save the changes back to the dictionary by clicking the 'Save Changes' button.",
            InfoMessageType.Warning)]
        private InspectorPlaceholder _skinDictionaryInfo;
        [TitleGroup("Skins")]
        [field: OdinSerialize]
        private Dictionary<BlockTypes, string> skinDictionary = new();
        [TitleGroup("Skins")]
        [SerializeField, HideIf("@deconstructed.Count == 0")] private List<SkinWrapper> deconstructed = new();
        [TitleGroup("Skins")]
        [Button("Deconstruct")]
        private void Deconstruct()
        {
            deconstructed = new List<SkinWrapper>();
            foreach (var kvp in skinDictionary)
            {
                deconstructed.Add(new SkinWrapper(skeletonAnimation, kvp.Key, kvp.Value));
            }
        }
        [TitleGroup("Skins")]
        [Button("Save Changes")]
        private void SaveChanges()
        {
            skinDictionary = new Dictionary<BlockTypes, string>();
            foreach (var wrapper in deconstructed)
            {
                skinDictionary.Add(wrapper.blockType, wrapper.skinName);
            }
            deconstructed.Clear();
        }
        #endregion

        #region Fields and Properties
        private BlockTypes _blockType;
        private MeshRenderer _meshRenderer;
        private Vector3 _originalScale;
        private Tween _pickUpTween;
        private IDisposable _switchIdleTimer;
        private CancellationTokenSource _switchIdleCts;
        #endregion
        
        #region Initalization
        private void Awake()
        {
            if (!skeletonAnimation)
            {
                Debug.LogError("SkeletonAnimation is not assigned in BlockView.");
                return;
            }
            if (!skeletonAnimation.TryGetComponent(out _meshRenderer))
            {
                Debug.LogError("MeshRenderer is not found on SkeletonAnimation.");
                return;
            }
            if (!infectedSpriteRenderer)
            {
                Debug.LogWarning("InfectedSpriteRenderer is not assigned in BlockView. Infected state will not be visible.");
            }
            else
            {
                infectedSpriteRenderer.enabled = false;
            }
            _originalScale = transform.localScale;
            skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[0], true);
            StartIdleTimer();
        }
        #endregion

        #region Utils
        private void StartIdleTimer()
        {
            var randomSwitchTime = UnityEngine.Random.Range(switchIdleTimeRange.x, switchIdleTimeRange.y);
            _switchIdleCts = new CancellationTokenSource();
            _switchIdleTimer = Observable.Timer(TimeSpan.FromSeconds(randomSwitchTime), _switchIdleCts.Token)
                .Subscribe(_ =>
                {
                    skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[1], true);
                    skeletonAnimation.AnimationState.AddAnimation(0, idleAnimations[0], true, 0f);
                    CancelIdleTimer();
                    StartIdleTimer();
                });
        }
        
        private void CancelIdleTimer()
        {
            _switchIdleTimer?.Dispose();
            _switchIdleCts?.Cancel();
            _switchIdleCts?.Dispose();
            _switchIdleTimer = null;
            _switchIdleCts = null;
        }

        private void OnDestroy()
        {
            CancelIdleTimer();
        }

        public void PickUp()
        {
            CancelIdleTimer();
            _pickUpTween = Tween.Scale(transform, _originalScale * pickUpScaleMultiplier, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, pickUpAnimation, true);
        }
        
        public void Place()
        {
            _pickUpTween.Stop();
            _pickUpTween = Tween.Scale(transform, _originalScale, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[0], true);
            StartIdleTimer();
        }
        
        public async UniTask Explode(FitType fitType)
        {
            CancelIdleTimer();
            var speedMultiplier = fitType == FitType.FitMe ? 2f : 6.67f;
            var explodeAnim = skeletonAnimation.AnimationState.SetAnimation(0, explodeAnimation, false);
            explodeAnim.TimeScale *= speedMultiplier;
            await explodeAnim.ToUniTask();
            //await UniTask.WaitUntil(() => skeletonAnimation.AnimationState.GetCurrent(0).IsComplete);
            if (explodeVfx.TryGetValue(_blockType, out var vfx))
            {
                var vfxInstance = Instantiate(vfx, transform.position, Quaternion.identity);
                vfxInstance.Play(true);
            }
            else
            {
                Debug.LogWarning($"No explosion VFX found for block type: {_blockType}");
            }
        }

        public void SetType(BlockTypes type)
        {
            if (!skinDictionary.TryGetValue(type, out var skin))
            {
                Debug.LogWarning($"No skin found for block type: {type}");
                 return;
            }
            _blockType = type;
            skeletonAnimation.Skeleton.SetSkin(skin);
            skeletonAnimation.Skeleton.SetSlotsToSetupPose();
        }

        public void SetSortingLayer(int layer)
        {
            _meshRenderer.sortingLayerID = layer;
            if (infectedSpriteRenderer)
            {
                infectedSpriteRenderer.sortingLayerID = layer;
            }
            else
            {
                Debug.LogWarning("InfectedSpriteRenderer is not assigned. Sorting layer will not be set for infected sprite.");
            }
        }
        
        public void SetSortingOrder(int order)
        {
            _meshRenderer.sortingOrder = order;
            if (infectedSpriteRenderer)
            {
                infectedSpriteRenderer.sortingOrder = order;
            }
            else
            {
                Debug.LogWarning("InfectedSpriteRenderer is not assigned. Sorting order will not be set for infected sprite.");
            }
        }

        public void ChangeSortingOrder(int change)
        {
            _meshRenderer.sortingOrder += change;
            if (infectedSpriteRenderer)
            {
                infectedSpriteRenderer.sortingOrder += change;
            }
            else
            {
                Debug.LogWarning("InfectedSpriteRenderer is not assigned. Sorting order will not be changed for infected sprite.");
            }
        }

        public void SetColor(Color color)
        {
            skeletonAnimation.Skeleton.SetColor(color);
        }

        public void PreInfect()
        {
            CancelIdleTimer();
            skeletonAnimation.AnimationState.SetAnimation(0, preInfectedAnimation, true);
        }

        public void Infect()
        {
            CancelIdleTimer();
            if (infectedSpriteRenderer)
            {
                infectedSpriteRenderer.enabled = true;
                _meshRenderer.enabled = false;
                skeletonAnimation.AnimationState.ClearTrack(0);
                skeletonAnimation.AnimationState.SetEmptyAnimation(0, 0);
            }
            else
            {
                Debug.LogWarning("InfectedSpriteRenderer is not assigned. Infected sprite will not be visible.");
            }
        }
        #endregion
    }
}
