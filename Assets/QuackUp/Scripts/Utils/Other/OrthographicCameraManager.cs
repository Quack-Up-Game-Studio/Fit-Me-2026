using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
     public class OrthographicCameraManager : IDisposable
    {
        [Title("Debug"),
            HideLabel,
            ShowInInspector] private InspectorPlaceholder _debugPlaceholder;
        
        private readonly Camera _mainCamera;
        public Camera MainCamera => _mainCamera;
        
        private Sequence _shakeSequence;

        [Inject]
        public OrthographicCameraManager(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }

        public void Dispose()
        {
            
        }

        [HideInEditorMode,
         Button("Shake")]
        public UniTask Shake(ShakeSettings shakeSettings, float strengthFactor)
        {
            _shakeSequence = Sequence.Create()
                .Group(Tween.ShakeCamera(_mainCamera, strengthFactor, shakeSettings.duration, shakeSettings.frequency,
                    shakeSettings.startDelay, shakeSettings.endDelay, shakeSettings.useUnscaledTime));
            return _shakeSequence.ToYieldInstruction().ToUniTask();
        }
        
        [HideInEditorMode,
         Button("Stop Shake")]
        public void StopShake(bool complete = true)
        {
            if (complete)
            {
                _shakeSequence.Complete();
            }
            else
            {
                _shakeSequence.Stop();
            }
        }

        [HideInEditorMode,
         Button("Change Orthographic Size")]
        public UniTask ChangeOrthographicSize(TweenSettings<float> tweenSettings)
        {
            var sequence = Sequence.Create()
                .Group(Tween.CameraOrthographicSize(_mainCamera, tweenSettings));
            return sequence.ToYieldInstruction().ToUniTask();
        }
    }
    
    [Serializable]
    public class OrthographicCameraManagerDebugData : DebugDataBase
    {
        [ShowInInspector] private OrthographicCameraManager _manager;
        
        public OrthographicCameraManagerDebugData(
            OrthographicCameraManager manager)
        {
            _manager = manager;
        }
    }

    [Serializable]
    public class OrthographicCameraManagerInstaller : DebugableInstaller<OrthographicCameraManagerDebugData>
    {
        [Title("Camera Manager")] 
        [Required, 
         SerializeField] private Camera mainCamera;
        
        public override void Install(IContainerBuilder builder)
        {
            builder.Register(x =>
            {
                var manager = new OrthographicCameraManager(mainCamera);
                return manager;
            }, Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                var manager = x.Resolve<OrthographicCameraManager>();
                DebugData = new OrthographicCameraManagerDebugData(manager);
            });
        }
    }
}