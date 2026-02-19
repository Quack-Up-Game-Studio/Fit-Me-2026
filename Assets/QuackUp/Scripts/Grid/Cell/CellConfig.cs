using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "CellConfig", menuName = "FitMe/Grid/Cell/CellConfig")]
    public class CellConfig : SerializedScriptableObject
    {
        [field: SerializeField] public bool UseDedicatedSprite { get; private set; } = true;
        [field: SerializeField] public Sprite[] WhitePatterns { get; private set; }
        [field: SerializeField] public Sprite[] BlackPatterns { get; private set; }
        [field: SerializeField] public Color WhiteColor { get; private set; } = Color.white;
        [field: SerializeField] public Color BlackColor { get; private set; } = Color.black;
        [field: SerializeField] public Color CanBePlacedColor { get; private set; } = Color.green;
        [field: SerializeField] public Color CannotBePlacedColor { get; private set; } = Color.red;
    }
}