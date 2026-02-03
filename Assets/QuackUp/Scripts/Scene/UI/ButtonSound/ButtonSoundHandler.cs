using System;
using FMODUnity;
using QuackUp.Audio;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Scene.UI
{
   
    public class ButtonSoundHandler : MonoBehaviour, IDisposable
    { 
        [SerializeField] private Button button;
        [SerializeField] private EventReference buttonClickSound;
        
        private IAudioManager _audioManager;
        private IDisposable _bindings;

        [Inject]
        public void Construct(IAudioManager audioManager)
        {
            _audioManager = audioManager;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            button.OnClickAsObservable()
                .Subscribe(_ => PlayButtonClickSound())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
        
        private void PlayButtonClickSound()
        {
            _audioManager.PlayAudioOneShot(buttonClickSound, transform.position);
        }
    }
}