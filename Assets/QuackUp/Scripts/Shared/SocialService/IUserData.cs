using UnityEngine;

namespace FitMe.Shared
{
    public interface IUserDataProvider
    {
        Sprite Avatar { get; }
        string DisplayName { get; }
    }
}