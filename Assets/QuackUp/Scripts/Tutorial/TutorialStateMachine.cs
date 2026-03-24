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

        public void StartTutorial()
        {
            JumpTo(0).Forget();
        }

        public void CompleteTutorial()
        {
            _onTutorialCompleted.OnNext(Unit.Default);
        }
    }
}