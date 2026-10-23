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
        [field: SerializeField] public Color HintFlashColor { get; private set; } = Color.yellow;
        [field: SerializeField, Range(0f, 1f), Tooltip("Strength of the screen-blend overlay for hints and placement previews.")]
        public float OverlayAmount { get; private set; } = 0.5f;
        
        [field: Title("Outline / Base (Layer 3)")]
        [field: SerializeField] public bool EnableBaseOutline { get; private set; } = true;
        [field: SerializeField, Tooltip("Checked: smooth continuous board boundary. Unchecked: complete rounded outline for each cell. Both stay behind all floor fills; opaque neighbours hide shared sides.")]
        public bool CheckAdjacentCellsForOutline { get; private set; } = true;

        [field: SerializeField] public Color BaseColor { get; private set; } = new Color(0.74f, 1.0f, 0.28f, 1.0f);
        [field: SerializeField, Range(0f, .5f)] public float BaseCornerRadius { get; private set; } = .12f;
        // Legacy serialized values retained for compatibility; no longer used by the renderer.
        [field: SerializeField, HideInInspector]
        public float BaseSeamInsetDepth { get; private set; } = .025f;
        [field: SerializeField, HideInInspector]
        public float BaseSeamInsetWidth { get; private set; } = .16f;
        [field: SerializeField, Range(1.0f, 2.0f)] public float BaseScale { get; private set; } = 1.15f;
        [field: SerializeField] public int BaseSortingOrderOffset { get; private set; } = -10;
    }
}