using System;
using R3;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
#endif
using UnityEngine;

namespace QuackUp.Utils
{
    public interface IDebugData
    {
        public bool ConstantUpdate { get; }
        public bool AutoCloseWhenPlayModeEnds { get; }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Debug window for inspecting IDebugData objects.
    /// </summary>
    public class DebugEditorWindow : OdinEditorWindow
    {
        
        private IDebugData _debugData;
        private IDisposable _applicationQuitSubscription;
        public static DebugEditorWindow Inspect(IDebugData debugData, string title = "Debug")
        {
            var window = CreateWindow<DebugEditorWindow>();
            window._debugData = debugData;
            var inspectWindow = InspectObject(window, debugData);
            inspectWindow.titleContent = new GUIContent(title);
            return window;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _applicationQuitSubscription = Observable.EveryValueChanged(this, _ => Application.isPlaying)
                .DistinctUntilChanged()
                .Subscribe(isPlaying =>
                {
                    if (!isPlaying && _debugData.AutoCloseWhenPlayModeEnds)
                    {
                        Close();
                    }
                });
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            _applicationQuitSubscription?.Dispose();
        }

        protected void OnInspectorUpdate()
        {
            if (!_debugData.ConstantUpdate) return;
            Repaint();
        }
       
    }
#else
    public class DebugEditorWindow {}
#endif
}