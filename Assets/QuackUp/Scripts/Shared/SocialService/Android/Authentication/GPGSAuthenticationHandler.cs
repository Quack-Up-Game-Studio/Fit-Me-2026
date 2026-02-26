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
        public string DisplayName => IsAuthenticated ? PlayGamesPlatform.Instance.GetUserDisplayName() : "Guest";
        
        
        private readonly Subject<bool> _onAuthenticationResult = new();
        private readonly GPGSAuthenticationManager _authenticationManager;
        
        private Sprite _avatarCache;
        private IDisposable _subscriptions;
        
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
            _avatarCache = null; // Clear avatar cache on authentication result to ensure we fetch the correct avatar for the authenticated user
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
        
        public async UniTask<Sprite> GetAvatar()
        {
            if (!IsAuthenticated) return null;
            if (_avatarCache) return _avatarCache;
            var url = PlayGamesPlatform.Instance.GetUserImageUrl();
            if (string.IsNullOrEmpty(url)) return null;
            var texture = await Texture2DUtils.LoadTextureFromUrl(url);
            if (!texture) return null;
            _avatarCache = texture.ToSprite();
            return _avatarCache;
        }
    }
}