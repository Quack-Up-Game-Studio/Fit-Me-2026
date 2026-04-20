using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace QuackUp.Utils
{
    public struct DifficultyChangeEvent
    {
        public readonly float Difficulty;

        public DifficultyChangeEvent(float difficulty)
        {
            Difficulty = difficulty;
        }
    }
    
    [Serializable]
    public class BackGroundSettings
    {
        public Sprite BgSprite;
        public Material BgScrollMaterial;
        public float DifficultyThreshold;
    }
    
    public class BackGroundEffectView : MonoBehaviour, IDisposable
    {
        [SerializeField] private Image mainBg;
        [SerializeField] private Image mainScroll;
        [SerializeField] private Image fadeBg;
        [SerializeField] private Image fadeScroll;
        [SerializeField] private float _fadeSpeed = 1f;

        [SerializeField] private BackGroundSettings[] backGroundSettings = Array.Empty<BackGroundSettings>();
        private CancellationTokenSource _fadeCts;
        private IDisposable _subscriptions;
        
        [Inject]
        private void Construct(ISubscriber<DifficultyChangeEvent> difficultySubscriber)
        {
            var disposableBuilder = Disposable.CreateBuilder();
            difficultySubscriber.Subscribe(evt => UpdateBackGround(evt.Difficulty))
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            CancelCurrentFade();
            _subscriptions?.Dispose();
        }
        
        public void UpdateBackGround(float difficulty)
        {
            var config = backGroundSettings
                .OrderByDescending(x => x.DifficultyThreshold)
                .FirstOrDefault(x => difficulty >= x.DifficultyThreshold);

            if (config != null && config.BgSprite != mainBg.sprite)
            {
                CancelCurrentFade();
                _fadeCts = new CancellationTokenSource();
                FadeToNextBackground(config.BgSprite, config.BgScrollMaterial, _fadeCts.Token).Forget();
            }
        }
        
        private void CancelCurrentFade()
        {
            if (_fadeCts != null)
            {
                _fadeCts.Cancel();
                _fadeCts.Dispose();
                _fadeCts = null;
            }
        }
        
        private async UniTaskVoid FadeToNextBackground(Sprite nextSprite, Material nextScroll, CancellationToken token)
        {
            fadeBg.sprite = nextSprite;
            fadeScroll.material = nextScroll;
            fadeBg.color = new Color(1, 1, 1, 0);
            fadeScroll.color = new Color(1, 1, 1, 0);
            
            float alpha = 0;
        
            while (alpha < 1f && !token.IsCancellationRequested)
            {
                alpha += Time.deltaTime * _fadeSpeed;
                fadeBg.color = new Color(1, 1, 1, alpha);
                fadeScroll.color = new Color(1, 1, 1, alpha);
            
                await UniTask.Yield(PlayerLoopTiming.Update, token); 
            }

            if (!token.IsCancellationRequested)
            {
                mainBg.sprite = nextSprite;
                mainScroll.material = nextScroll;
                fadeBg.color = new Color(1, 1, 1, 0);
                fadeScroll.color = new Color(1, 1, 1, 0);
            }
        }
    }
}
