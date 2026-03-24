using System.Threading;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Panel
{
    public abstract class FloatingUIElement : SerializedMonoBehaviour, IAnimatable, ITransitionable
    {
        public abstract UniTask Animate(CancellationToken cancellationToken = default);

        public abstract UniTask TransitionIn(CancellationToken cancellationToken = default);

        public abstract UniTask TransitionOut(CancellationToken cancellationToken = default);

        public abstract void Reset();
    }   
}