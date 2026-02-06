using QuackUp.Save;
using UnityEngine;

namespace QuackUp.Audio
{
    [CreateAssetMenu(fileName = "AudioSaveObject", menuName = "QuackUp/Audio/AudioSaveObject", order = 0)]
    public class AudioSaveObject : MessagePackSaveObject<AudioSaveData>
    {
        public override void Reset()
        {
            base.Reset();
            saveData = new AudioSaveData();
        }
    }
}