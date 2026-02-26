using System;
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
    public class GPGSAuthenticationHandler : IAuthenticationService, IUserDataProvider, IDisposable
    {
        public Observable<bool> OnAuthenticationResult => _onAuthenticationResult;
        public bool IsAuthenticated => PlayGamesPlatform.Instance.IsAuthenticated();
        private readonly Subject<bool> _onAuthenticationResult = new();
        
        private readonly GPGSAuthenticationManager _authenticationManager;
        
        private IDisposable _subscriptions;
        
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
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _authenticationManager.OnAuthenticationResult
                .Subscribe(OnAuthenticationResultReceived)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        private void OnAuthenticationResultReceived(SignInStatus success)
        {
            _onAuthenticationResult.OnNext(success == SignInStatus.Success);
        }
        
        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public async UniTask<bool> Authenticate()
        {
            var result = await _authenticationManager.Authenticate();
            return result == SignInStatus.Success;
        }
    }
}