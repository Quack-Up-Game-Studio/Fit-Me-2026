using FMODUnity;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "BlockConfig", menuName = "FitMe/Grid/BlockConfig")]
    public class BlockConfig : SerializedScriptableObject
    {
        [Title("Block Settings")]
        [field: SerializeField] public bool UseAtomSprite { get; private set; }
        [field: SerializeField, SortingLayer] public int OriginalSortingLayer { get; private set; }
        [field: SerializeField, SortingLayer] public int PickUpSortingLayer { get; private set; }
        [field: SerializeField] public Color OriginalAtomColor { get; private set; } = Color.white;
        // [field: SerializeField] public Color InfectColor { get; private set; } = Color.gray;
        // [field: SerializeField] public float FlashDuration { get; private set; } = 0.2f;
        [field: SerializeField] public bool AllowPickUpAfterPlacement { get; private set; }
        
        [Title("Audios")] 
        [field: SerializeField] public EventReference PlaceSucceedSfx { get; private set; }
        [field: SerializeField] public EventReference PlaceFailSfx { get; private set; }
        // [field: SerializeField] public EventReference preInfectSfx;
        // [field: SerializeField] public EventReference infectSfx;
    }
}