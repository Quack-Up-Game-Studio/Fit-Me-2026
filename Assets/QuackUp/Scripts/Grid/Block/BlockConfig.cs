using System;
using System.Collections.Generic;
using FitMe.Shared;
using FMODUnity;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Serialization;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "BlockConfig", menuName = "FitMe/Grid/Block/BlockConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockConfig : SerializedScriptableObject, IHasSkeletonDataAsset
    {
        [Title("Block Settings")]
        [field: SerializeField] public bool UseAtomSprite { get; private set; }
        [field: SerializeField] public Sprite ObstacleSprite { get; private set; }
        
        [Title("Animations")]
        [field: SerializeField, SpineAnimation] private List<string> idleAnimations = new();
        public IReadOnlyList<string> IdleAnimations => idleAnimations;
        [field: SerializeField, SpineAnimation] public string PickUpAnimation { get; private set; }
        [field: SerializeField, SpineAnimation] public string ExplodeAnimation { get; private set; }
        //[SerializeField, SpineAnimation] string preInfectedAnimation;
        
        [Title("VFX")]
        [field: OdinSerialize] private Dictionary<BlockColor, ParticleSystem> _explodeVfx = new();
        public IReadOnlyDictionary<BlockColor, ParticleSystem> ExplodeVfx => _explodeVfx;
        [field: OdinSerialize] private Dictionary<BlockColor, ParticleSystem> _placeVfx = new();
        public IReadOnlyDictionary<BlockColor, ParticleSystem> PlaceVFX => _placeVfx;
        
        [TitleGroup("Skins")] 
        [SerializeField, Required] private SkeletonDataAsset skeletonDataAsset;
        public SkeletonDataAsset SkeletonDataAsset => skeletonDataAsset;
        [ShowInInspector, HideLabel]
        [DetailedInfoBox("Read Me",
            "Due to a certain limitation of Spine handling of the attributes, the skin selector cannot be drawn under dictionary, " +
            "and has to be deconstructed into a list of SkinWrapper objects.\n" +
            "You can deconstruct the dictionary into a list by clicking the 'Deconstruct' button, " +
            "and then save the changes back to the dictionary by clicking the 'Save Changes' button.",
            InfoMessageType.Warning)]
        private InspectorPlaceholder _skinDictionaryInfo;
        [TitleGroup("Skins")]
        [field: OdinSerialize] private Dictionary<BlockColor, string> skinDictionary = new();
        public IReadOnlyDictionary<BlockColor, string> SkinDictionary => skinDictionary;
        [TitleGroup("Skins")]
        [SerializeField, HideIf("@deconstructed.Count == 0"), TableList] private List<SkinWrapper> deconstructed = new();
        [TitleGroup("Skins")]
        [Button("Deconstruct"), ShowIf("@deconstructed.Count == 0")]
        private void Deconstruct()
        {
            deconstructed = new List<SkinWrapper>();
            foreach (var kvp in skinDictionary)
            {
                deconstructed.Add(new SkinWrapper(kvp.Key, kvp.Value));
            }
        }
        [TitleGroup("Skins")]
        [Button("Save Changes"), HideIf("@deconstructed.Count == 0")]
        private void SaveChanges()
        {
            skinDictionary = new Dictionary<BlockColor, string>();
            foreach (var wrapper in deconstructed)
            {
                skinDictionary.Add(wrapper.blockColor, wrapper.skinName);
            }
            deconstructed.Clear();
        }
        
        [Serializable]
        private record SkinWrapper
        {
            [ShowInInspector, ReadOnly] public BlockColor blockColor;
            [SpineSkin] public string skinName;
            
            public SkinWrapper(BlockColor blockType, string skinName)
            {
                blockColor = blockType;
                this.skinName = skinName;
            }
        }
    }
}