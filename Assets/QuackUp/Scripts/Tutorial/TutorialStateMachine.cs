using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;

namespace FitMe.Tutorial
{
    public class TutorialStateMachine : ExtendedStateMachine<TutorialState>
    {
        private readonly Subject<Unit> _onTutorialCompleted = new();
        public Observable<Unit> OnTutorialCompleted => _onTutorialCompleted;

        public void StartTutorial(string overrideStart = null)
        {
            if (!string.IsNullOrEmpty(overrideStart))
            {
                JumpTo(overrideStart).Forget();
            }
            else
            {
                JumpTo(0).Forget();
            }
        }

        public void CompleteTutorial()
        {
            _onTutorialCompleted.OnNext(Unit.Default);
        }
    }
}