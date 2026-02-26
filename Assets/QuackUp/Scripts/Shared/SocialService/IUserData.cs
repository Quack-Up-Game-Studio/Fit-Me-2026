using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FitMe.Shared
{
    public interface IUserDataProvider
    {
        UniTask<Sprite> GetAvatar();
        string DisplayName { get; }
    }
    
    public class MockUserDataProvider : IUserDataProvider
    {
        public string DisplayName => "Guest";
        public UniTask<Sprite> GetAvatar()
        {
            return UniTask.FromResult<Sprite>(null);
        }
    }
}