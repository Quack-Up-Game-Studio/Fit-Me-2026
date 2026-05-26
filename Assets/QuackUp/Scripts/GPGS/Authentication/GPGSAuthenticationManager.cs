#if UNITY_ANDROID
using Cysharp.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using R3;
using VContainer;
using VContainer.Unity;

namespace QuackUp.GPGS
{
    public class GPGSAuthenticationManager : IInitializable
    {

        public Observable<SignInStatus> OnAuthenticationResult => _onAuthenticationResult;
        private readonly Subject<SignInStatus> _onAuthenticationResult = new();
        
        private readonly GPGSAuthenticationManagerConfig _config;
        
        [Inject]
        public GPGSAuthenticationManager(GPGSAuthenticationManagerConfig config)
        {
            _config = config;
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.DebugLogEnabled = true;
#endif
        }
        
        public void Initialize()
        {
            if (!_config.AutoAuthenticateOnStart) return;
            AuthenticateSilently().Forget();
        }
        
        public UniTask<SignInStatus> AuthenticateSilently()
        {
            var tcs = new UniTaskCompletionSource<SignInStatus>();
            PlayGamesPlatform.Instance.Authenticate(
                (result) =>
                {
                    tcs.TrySetResult(result);
                    _onAuthenticationResult.OnNext(result);
                });
            return tcs.Task;
        }
        
        public UniTask<SignInStatus> Authenticate()
        {
            var tcs = new UniTaskCompletionSource<SignInStatus>();
            PlayGamesPlatform.Instance.ManuallyAuthenticate(
                (result) =>
                {
                    tcs.TrySetResult(result);
                    _onAuthenticationResult.OnNext(result);
                });
            return tcs.Task;
        }
    }
}
#endif
