using System;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.Shared
{
    public interface IAuthenticationService
    {
        UniTask<bool> Authenticate();
        Observable<bool> OnAuthenticationResult { get; }
        bool IsAuthenticated { get; }
    }
    
    [Serializable]
    public class MockAuthenticationServiceInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<MockAuthenticationService>(Lifetime.Singleton)
                .As<IAuthenticationService>();
        }
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