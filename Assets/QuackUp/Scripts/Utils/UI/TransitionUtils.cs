using System.Threading;
using Cysharp.Threading.Tasks;

namespace QuackUp.Utils
{
    public interface ITransitionable
    {
        UniTask TransitionIn(CancellationToken cancellationToken = default);
        UniTask TransitionOut(CancellationToken cancellationToken = default);
    }

    public interface IAnimatable
    {
        UniTask Animate(CancellationToken cancellationToken = default);
    }
    
    public class TransitionMock : ITransitionable
    {
        public UniTask TransitionIn(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        public UniTask TransitionOut(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

    }

    public class AnimateMock : IAnimatable
    {
        public UniTask Animate(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
    }
}