using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace QuackUp.Audio
{
    #region Data Structures
    
    public record AudioReference
    {
        public EventInstance eventInstance;
        public EventReference eventReference;
        public readonly string identifier;
        
        public AudioReference(EventInstance eventInstance, EventReference eventReference, string identifier = null)
        {
            this.eventInstance = eventInstance;
            this.eventReference = eventReference;
            this.identifier = identifier;
        }
    }
    #endregion
    
    #region Interfaces
    public interface IAudioManager
    {
        AudioReference PlayAudio(EventReference eventReference, Vector3 position, string id = null,
            Transform parent = null, bool addToWildIfNotId = true);
        void PlayAudioOneShot(EventReference eventReference, Vector3 position);
        void SetPauseAudio(AudioReference audioReference, bool pause);
        void SetPauseAllAudioInIdentifier(string id, bool pause);
        void SetPauseAllIndexedAudio(bool pause);
        void SetPauseAllWildAudio(bool pause);
        void SetPauseAllAudio(bool pause);
        void RegisterAudioReference(AudioReference audioReference, string id);
        void UnregisterAudioReference(string id);
        void StopAudio(AudioReference audioReference, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT);
        void StopAllAudioInIdentifier(string id);
        void StopAllIndexedAudio();
        void StopAllWildAudio();
        void StopAllAudio();
        bool TryFindAudioReference(string id, out AudioReference audioReference);
    }

    public interface IAudioBusManager
    {
        bool GetBusData(BusType busType, out BusData busData);
        bool GetBusMuteState(BusType busType, out bool isMuted);
        bool GetBusVolume(BusType busType, out float volume, VolumeUnit outUnit);
        void SetMuteBus(BusType busType, bool mute);
        void ToggleMuteBus(BusType busType);
        void SetVolumeBus(BusType busType, float value, VolumeUnit inUnit);
        void StopAllAudioInBus(BusType busType, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT);
    }
    #endregion
    
    #region Mocks
    public class AudioManagerMock : IAudioManager
    {
        public AudioReference PlayAudio(EventReference eventReference, Vector3 position, string id = null, Transform parent = null, bool addToWildIfNotId = true)
        {
            return new AudioReference(new EventInstance(), new EventReference());
        }
        public void PlayAudioOneShot(EventReference eventReference, Vector3 position){ }
        public void SetPauseAudio(AudioReference audioReference, bool pause){ }
        public void SetPauseAllAudioInIdentifier(string id, bool pause){ }
        public void SetPauseAllIndexedAudio(bool pause){ }
        public void SetPauseAllWildAudio(bool pause){ }
        public void SetPauseAllAudio(bool pause){ }
        public void RegisterAudioReference(AudioReference audioReference, string id){}
        public void UnregisterAudioReference(string id){}

        public void StopAudio(AudioReference audioReference, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT){ }
        public void StopAllAudioInIdentifier(string id){ }
        public void StopAllIndexedAudio(){ }
        public void StopAllWildAudio(){ }
        public void StopAllAudio(){ }
        public bool TryFindAudioReference(string id, out AudioReference audioReference)
        {
            audioReference = null;
            return false;
        }
    }
    
    public class AudioBusManagerMock : IAudioBusManager
    {
        public bool GetBusData(BusType busType, out BusData busData)
        {
            busData = null;
            return false;
        }
        public bool GetBusMuteState(BusType busType, out bool isMuted)
        {
            isMuted = false;
            return false;
        }
        public bool GetBusVolume(BusType busType, out float volume, VolumeUnit outUnit)
        {
            volume = 0f;
            return false;
        }
        public void SetMuteBus(BusType busType, bool mute){ }
        public void ToggleMuteBus(BusType busType){ }
        public void SetVolumeBus(BusType busType, float value, VolumeUnit inUnit){ }
        public void StopAllAudioInBus(BusType busType, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT){ }
    }
    #endregion
}