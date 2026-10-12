using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using QuackUp.SocialService;

namespace QuackUp.iOSSocial
{
    public class iOSAuthenticationService : IAuthenticationService
    {
        private readonly Subject<bool> _onAuthenticationResult = new();
        public Observable<bool> OnAuthenticationResult => _onAuthenticationResult;
        public bool IsAuthenticated => Social.localUser.authenticated;
        public UniTask<bool> Authenticate(CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource<bool>();
            cancellationToken.Register(() => tcs.TrySetCanceled());
            Social.localUser.Authenticate(success =>
            {
                var result = success && Social.localUser.authenticated;
                _onAuthenticationResult.OnNext(result);
                tcs.TrySetResult(result);
            });
            return tcs.Task;
        }

    }
}
