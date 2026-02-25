using Cysharp.Threading.Tasks;
using FitMe.Shared;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using QuackUp.GPGS;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace FitMe.SocialService.Android
{
    public class GPGSAuthenticationHandler : IAuthenticationService, IUserDataProvider
    {
        public Observable<bool> OnAuthenticationResult => _onAuthenticationResult;
        public bool IsAuthenticated => PlayGamesPlatform.Instance.IsAuthenticated();
        private readonly Subject<bool> _onAuthenticationResult = new();
        
        private readonly GPGSAuthenticationManager _authenticationManager;
        
        public Sprite Avatar
        {
            get
            {
                if (!PlayGamesPlatform.Instance.IsAuthenticated()) return null;
                return PlayGamesPlatform.Instance.localUser.image.ToSprite();
            }
        }
        
        public string DisplayName
        {
            get
            {
                if (!PlayGamesPlatform.Instance.IsAuthenticated()) return null;
                return PlayGamesPlatform.Instance.localUser.userName;
            }
        }
        
        public GPGSAuthenticationHandler(
            GPGSAuthenticationManager authenticationManager)
        {
            _authenticationManager = authenticationManager;
        }

        public async UniTask<bool> Authenticate()
        {
            var result = await _authenticationManager.Authenticate();
            var success = result == SignInStatus.Success;
            _onAuthenticationResult.OnNext(success);
            return success;
        }
    }
}