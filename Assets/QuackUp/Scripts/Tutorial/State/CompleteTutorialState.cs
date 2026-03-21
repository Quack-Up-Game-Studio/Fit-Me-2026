using Cysharp.Threading.Tasks;

namespace FitMe.Tutorial
{
    public class CompleteTutorialState : TutorialState
    {
        public override async UniTask Enter()
        {
            await base.Enter();
            StateMachine.CompleteTutorial();
        }
    }
}