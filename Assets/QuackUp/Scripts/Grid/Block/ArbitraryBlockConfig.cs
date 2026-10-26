using System.Collections.Generic;
using FitMe.Shared;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Spine.Unity;
using UnityEngine;

namespace FitMe.Grid
{
    public enum ArbitraryBlockDesign
    {
        D1,
        D2,
        D3,
        D4
    }

    public enum ArbitraryBlockAnimation
    {
        Idle1,
        Idle2,
        Drag,
        Drop,
        Explode
    }

    [CreateAssetMenu(fileName = "ArbitraryBlockConfig", menuName = "FitMe/Grid/Block/ArbitraryBlockConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public sealed class ArbitraryBlockConfig : SerializedScriptableObject, IBlockPresentationConfig
    {
        [Title("Atom Spine Designs")]
        [field: OdinSerialize] private Dictionary<ArbitraryBlockDesign, SkeletonDataAsset> _skeletonDataAssets = new();
        public IReadOnlyDictionary<ArbitraryBlockDesign, SkeletonDataAsset> SkeletonDataAssets => _skeletonDataAssets;

        [Title("Atom Skins")]
        [field: OdinSerialize] private Dictionary<BlockColor, string> _skinDictionary = new();
        public IReadOnlyDictionary<BlockColor, string> SkinDictionary => _skinDictionary;

        [Title("Atom Animations")]
        [field: OdinSerialize] private Dictionary<ArbitraryBlockAnimation, string> _animationDictionary = new();
        public IReadOnlyDictionary<ArbitraryBlockAnimation, string> AnimationDictionary => _animationDictionary;

        [Title("Block Effects")]
        [field: OdinSerialize] private Dictionary<BlockColor, ParticleSystem> _explodeVfx = new();
        public IReadOnlyDictionary<BlockColor, ParticleSystem> ExplodeVfx => _explodeVfx;
        [field: OdinSerialize] private Dictionary<BlockColor, ParticleSystem> _placeVfx = new();
        public IReadOnlyDictionary<BlockColor, ParticleSystem> PlaceVFX => _placeVfx;

        [Title("Obstacles")]
        [field: SerializeField] public Sprite ObstacleSprite { get; private set; }
        [field: SerializeField] public Color ObstacleAtomSpriteColor { get; private set; } = Color.gray;

        [Title("Atom Presentation")]
        [field: SerializeField] public bool UseAtomSprite { get; private set; }
        public bool UsesAtomRenderer => true;
        public SkeletonDataAsset SkeletonDataAsset => GetSkeletonDataAsset(ArbitraryBlockDesign.D1);
        public string PickUpAnimation => TryGetAnimation(ArbitraryBlockAnimation.Drag, out var animation) ? animation : null;
        public string ExplodeAnimation => TryGetAnimation(ArbitraryBlockAnimation.Explode, out var animation) ? animation : null;

        public IReadOnlyList<string> IdleAnimations
        {
            get
            {
                var idleAnimations = new List<string>(2);
                if (TryGetAnimation(ArbitraryBlockAnimation.Idle1, out var idle1)) idleAnimations.Add(idle1);
                if (TryGetAnimation(ArbitraryBlockAnimation.Idle2, out var idle2)) idleAnimations.Add(idle2);
                return idleAnimations;
            }
        }

        public SkeletonDataAsset GetSkeletonDataAsset(ArbitraryBlockDesign design)
        {
            return _skeletonDataAssets.TryGetValue(design, out var asset) ? asset : null;
        }

        public bool TryGetSkin(BlockColor color, out string skinName)
        {
            return _skinDictionary.TryGetValue(color, out skinName);
        }

        public bool TryGetAnimation(ArbitraryBlockAnimation animation, out string animationName)
        {
            return _animationDictionary.TryGetValue(animation, out animationName);
        }
    }
}
