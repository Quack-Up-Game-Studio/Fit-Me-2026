using FitMe.Shared;
using GooglePlayGames;
using QuackUp.Utils;
using UnityEngine;

namespace FitMe.SocialService.Android
{
    public class GPGSUserDataProvider : IUserDataProvider
    {
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
    }
}