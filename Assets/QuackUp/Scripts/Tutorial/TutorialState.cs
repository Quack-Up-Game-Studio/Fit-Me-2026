using QuackUp.Utils;
using VContainer;

namespace FitMe.Tutorial
{
    public abstract class TutorialState : State
    {
        protected TutorialStateMachine StateMachine;
        
        [Inject]
        public void Initialize(TutorialStateMachine stateMachine)
        {
            StateMachine = stateMachine;
        }
    }
}