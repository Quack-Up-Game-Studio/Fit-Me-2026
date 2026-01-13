using Cysharp.Threading.Tasks;

namespace QuackUp.Utils
{
    public abstract class State
    {
        /// <summary>
        /// Call when entering the state.
        /// </summary>
        public virtual async UniTask Enter() { await UniTask.CompletedTask; }
        /// <summary>
        /// Call every frame while in the state.
        /// </summary>
        public virtual void Update() { }
        /// <summary>
        /// Call when exiting the state.
        /// </summary>
        public virtual async UniTask Exit() { await UniTask.CompletedTask; }
        /// <summary>
        /// Reset the state to its initial condition.
        /// </summary>
        public virtual void Reset() { }
        /// <summary>
        /// Complete the state, applying completion logic.
        /// </summary>
        public virtual void Complete() { }
        /// <summary>
        /// Handle failure within the state, applying failure logic.
        /// </summary>
        public virtual void Fail() { }
    }
    
    public abstract class StateMachine
    {
        private State _currentState;

        /// <summary>
        /// Changes the current state of the state machine.
        /// </summary>
        /// <param name="newState">New state to change to.</param>
        protected async UniTask ChangeState(State newState)
        {
            if (_currentState != null)
                await _currentState.Exit();
            _currentState = newState;
            await _currentState.Enter();
        }

        public void Update()
        {
            _currentState?.Update();
        }
    }
}
