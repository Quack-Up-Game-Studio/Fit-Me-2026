using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.Audio
{
    [CreateAssetMenu(fileName = "AudioManagerConfig", menuName = "QuackUp/Audio/AudioManagerConfig", order = 0)]
    public class AudioManagerConfig : ScriptableObject
    {
        [Title("References")] 
        [field: SerializeField] public AudioSettings AudioSettings { get; private set; }

        [Title("Settings")] 
        [field: SerializeField] public bool LimitAudioCount { get; private set; } = true;
        [field: SerializeField] public int MaxAudioCount { get; private set; } = 50;
    }
}