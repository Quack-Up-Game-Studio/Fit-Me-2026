using UnityEngine;

namespace FitMe.Shared
{
    public interface IUserDataProvider
    {
        Sprite Avatar { get; }
        string DisplayName { get; }
    }
    
    public class MockUserDataProvider : IUserDataProvider
    {
        public Sprite Avatar => null;
        public string DisplayName => "Mock User";
    }
}