using Cysharp.Threading.Tasks;
using R3;

namespace QuackUp.SocialService
{
    public interface IAuthenticationService
    {
        UniTask<bool> Authenticate();
        Observable<bool> OnAuthenticationResult { get; }
        bool IsAuthenticated { get; }
    }

    public class MockAuthenticationService : IAuthenticationService
    {
        public Observable<bool> OnAuthenticationResult => _onAuthenticationResult;
        public bool IsAuthenticated => false;
        private readonly Subject<bool> _onAuthenticationResult = new();

        public UniTask<bool> Authenticate()
        {
            _onAuthenticationResult.OnNext(false);
            return UniTask.FromResult(false);
        }
    }
}