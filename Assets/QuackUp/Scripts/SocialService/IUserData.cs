using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using UnityEngine;

namespace QuackUp.SocialService
{
    public interface IUserDataProvider
    {
        UniTask<Sprite> GetAvatar();
        public string AvatarUrl { get; }
        string DisplayName { get; }
        string UserId { get; }
    }
    
    public class MockUserDataProvider : IUserDataProvider
    {
        public string DisplayName => "Guest";
        public string UserId => "guest";
        public UniTask<Sprite> GetAvatar()
        {
            return UniTask.FromResult<Sprite>(null);
        }

        public string AvatarUrl => string.Empty;
    }

    public class UserData : IUserDataProvider
    {
        public async UniTask<Sprite> GetAvatar()
        {
            if (_avatarCache) 
                return _avatarCache;
            if (string.IsNullOrEmpty(AvatarUrl))
                return null;
            var sprite = (await Texture2DUtils.LoadTextureFromUrl(AvatarUrl)).ToSprite();
            _avatarCache = sprite;
            return sprite;
        }
        private Sprite _avatarCache;
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public string UserId { get; set; }
    }
}