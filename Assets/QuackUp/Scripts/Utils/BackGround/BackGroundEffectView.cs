using System;
using System.Linq;
using System.Threading;
using Coffee.UIExtensions;
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
        public Sprite BgScrollMaterial;
        public float DifficultyThreshold;
    }
    
    public class BackGroundEffectView : MonoBehaviour, IDisposable
    {
        [SerializeField] private Image mainBg;
        [SerializeField] private UIMaterialPropertyInjector mainScroll;
        [SerializeField] private Image fadeBg;
        [SerializeField] private UIMaterialPropertyInjector fadeScroll;
        [SerializeField] private float fadeSpeed = 1f;

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
        
        private async UniTaskVoid FadeToNextBackground(Sprite nextSprite, Sprite nextScroll, CancellationToken token)
        {
            fadeBg.sprite = nextSprite;
            fadeScroll.SetTexture("_MainTexture", nextScroll.texture);
            fadeBg.color = new Color(1, 1, 1, 0);
            fadeScroll.SetFloat("_Alpha", 0);
            
            float alpha = 0;
        
            while (alpha < 1f && !token.IsCancellationRequested)
            {
                alpha += Time.deltaTime * fadeSpeed;
                fadeBg.color = new Color(1, 1, 1, alpha);
                fadeScroll.SetFloat("_Alpha",alpha);
            
                await UniTask.Yield(PlayerLoopTiming.Update, token); 
            }

            if (!token.IsCancellationRequested)
            {
                mainBg.sprite = nextSprite;
                mainScroll.SetTexture("_MainTexture", nextScroll.texture);
                fadeBg.color = new Color(1, 1, 1, 0);
                fadeScroll.SetFloat("_Alpha", 0);
            }
        }
    }
}
