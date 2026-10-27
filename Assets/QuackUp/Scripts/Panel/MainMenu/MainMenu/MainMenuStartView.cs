using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public enum MainMenuStartMode
    {
        DragToStart = 0,
        PressToStart = 1,
    }

    [ExecuteAlways]
    public sealed class MainMenuStartView : MonoBehaviour
    {
        [SerializeField] private MainMenuStartMode startMode;
        [SerializeField] private Button pressStartButton;
        [Tooltip("Existing drag-only presentation. Kept intact when using Press To Start.")]
        [SerializeField] private GameObject[] dragStartObjects = System.Array.Empty<GameObject>();

        public MainMenuStartMode Mode => startMode;
        public Button PressStartButton => pressStartButton;
        public event System.Action<MainMenuStartMode> ModeChanged;
        private bool _needsRefresh;

        private void OnEnable() => ApplyMode();

        private void OnValidate() => _needsRefresh = true;

        private void Update()
        {
            if (_needsRefresh) ApplyMode();
        }

        public void ApplyMode()
        {
            _needsRefresh = false;
            var useDrag = startMode == MainMenuStartMode.DragToStart;
            if (pressStartButton) pressStartButton.gameObject.SetActive(!useDrag);
            foreach (var item in dragStartObjects)
                if (item) item.SetActive(useDrag);
            ModeChanged?.Invoke(startMode);
        }
    }
}
