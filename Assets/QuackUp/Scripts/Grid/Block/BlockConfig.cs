using System.Collections.Generic;
using FMODUnity;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Serialization;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "BlockConfig", menuName = "FitMe/Block/BlockConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockConfig : SerializedScriptableObject
    {
        [Title("Block Settings")]
        [field: SerializeField] public bool UseAtomSprite { get; private set; }
        [field: SerializeField, SortingLayer] public int OriginalSortingLayer { get; private set; }
        [field: SerializeField, SortingLayer] public int PickUpSortingLayer { get; private set; }
        [field: SerializeField] public Color OriginalAtomColor { get; private set; } = Color.white;
        [field: OdinSerialize] private Dictionary<BlockColor, Color> _atomColorDict = new();
        public IReadOnlyDictionary<BlockColor, Color> AtomColorDict => _atomColorDict;
        [field: SerializeField] public bool AllowPickUpAfterPlacement { get; private set; }
        [field: SerializeField] public bool RotateClockwise { get; private set; } = true;
        
        [Title("Audios")] 
        [field: SerializeField] public EventReference PlaceSucceedSfx { get; private set; }
        [field: SerializeField] public EventReference PlaceFailSfx { get; private set; }
        
        [Title("VFX")]
        [field: OdinSerialize] private Dictionary<BlockColor, ParticleSystem> _explodeVfx = new();
        public IReadOnlyDictionary<BlockColor, ParticleSystem> ExplodeVfx => _explodeVfx;
    }
}