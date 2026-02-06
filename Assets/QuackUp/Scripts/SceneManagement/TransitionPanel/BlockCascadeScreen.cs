using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace QuackUp.SceneManagement
{
    public class BlockCascadeScreen : MonoBehaviour, ITransitionable
    {
        [Serializable]
        private struct BlockTween
        {
            public RectTransform block;
            public TweenSettings<Vector2> positionTweenSettings;
            
            [Button("Set End Value")]
            private void SetEndValue(float startYPos)
            {
                positionTweenSettings.startValue = block.anchoredPosition.WithY(startYPos);
                positionTweenSettings.endValue = block.anchoredPosition;
            }
        }
        
        [Title("References")]
        [SerializeField] private List<BlockTween> blockTweens;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private TweenSettings<float> backgroundFadeInSettings;
        [SerializeField] private TweenSettings<float> backgroundFadeOutSettings;
        
        [Title("Settings")]
        [SerializeField] private bool useCombinedTime = true;
        [SerializeField, ShowIf(nameof(useCombinedTime))] private float combinedTime = 0.5f;
        
        [Title("Debug")]
        [Button("Transition In")]
        private void TransitionInDebug() => TransitionIn().Forget();
        [Button("Transition Out")]
        private void TransitionOutDebug() => TransitionOut().Forget();
        
        private Sequence _blockSequence;

        private void Awake()
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            background.color = background.color.WithA(backgroundFadeInSettings.startValue);
            foreach (var blockTween in blockTweens)
            {
                blockTween.block.anchoredPosition = blockTween.positionTweenSettings.startValue;
            }
        }

        public async UniTask TransitionIn(CancellationToken cancellationToken = default)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            cancellationToken.Register(CancelTransition);
            background.color = background.color.WithA(backgroundFadeInSettings.startValue);
            _blockSequence = Sequence.Create();
            var duration = combinedTime / blockTweens.Count;
            var blockSequence = Sequence.Create();
            foreach (var blockTween in blockTweens)
            {
                TweenSettings<Vector2> settings;
                if (useCombinedTime)
                {
                    var copy = blockTween.positionTweenSettings;
                    copy.settings.duration = duration;
                    settings = copy;
                }
                else
                {
                    settings = blockTween.positionTweenSettings;
                }
                _ = blockSequence.Chain(Tween.UIAnchoredPosition(blockTween.block, settings));
            }
            _ = _blockSequence.Group(blockSequence);
            _ = _blockSequence.Chain(Tween.Alpha(background, backgroundFadeInSettings));
            await _blockSequence.ToUniTask(cancellationToken: cancellationToken);
        }

        public async UniTask TransitionOut(CancellationToken cancellationToken = default)
        {
            cancellationToken.Register(CancelTransition);
            var reverseTweens = new List<BlockTween>(blockTweens);
            reverseTweens.Reverse();
            _blockSequence = Sequence.Create();
            _ = _blockSequence.Group(Tween.Alpha(background, backgroundFadeOutSettings));
            var duration = combinedTime / reverseTweens.Count;
            var blockSequence = Sequence.Create();
            foreach (var blockTween in reverseTweens)
            {
                TweenSettings<Vector2> settings;
                if (useCombinedTime)
                {
                    var copy = blockTween.positionTweenSettings;
                    copy.settings.duration = duration;
                    settings = copy;
                }
                else
                {
                    settings = blockTween.positionTweenSettings;
                }
                _ = blockSequence.Chain(Tween.UIAnchoredPosition(blockTween.block, settings.WithDirection(false)));
            }
            _ = _blockSequence.Group(blockSequence);
            await _blockSequence.ToUniTask(cancellationToken: cancellationToken);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void CancelTransition()
        {
            _blockSequence.Stop();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}