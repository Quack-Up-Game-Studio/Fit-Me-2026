using System;
using Cysharp.Threading.Tasks;
using QuackUp.SocialService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class LeaderboardBlock : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text fitText;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text rankText;
        
        public void SetData(Sprite avatar, string username, long score, long fit, DateTime date, int rank)
        {
            if (avatar)
                avatarImage.sprite = avatar;
            SetDataInternal(username, score, fit, date, rank);
        }
        
        public void SetData(IUserDataProvider userDataProvider, string username, long score, long fit, DateTime date, int rank)
        {
            LoadAvatar(userDataProvider).Forget();
            SetDataInternal(username, score, fit, date, rank);
        }

        private void SetDataInternal(string username, long score, long fit, DateTime date, int rank)
        {
            nameText.text = username;
            scoreText.text = $"{score:N0}";
            fitText.text = $"{fit:N0}";
            if (dateText) dateText.text = date.ToLocalTime().ToString("dd-MM-yyyy HH:mm");
            if (rankText) rankText.text = $"{rank}";
        }

        private async UniTaskVoid LoadAvatar(IUserDataProvider userDataProvider)
        {
            var avatar = await userDataProvider.GetAvatar();
            if (!avatar) return;
            avatarImage.sprite = avatar;
        }
    }
}