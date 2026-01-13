using System;
using System.Collections.Generic;
using System.Linq;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using QuackUp.Save;
using QuackUp.Utils;
using Redcode.Extensions;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Debug = UnityEngine.Debug;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace QuackUp.Audio
{
    public class AudioManager : 
        IAudioManager,
        IAudioBusManager,
        IInitializable, 
        IPostInitializable,
        IDisposable
    {
        private readonly Dictionary<string, List<AudioReference>> _indexedAudioReferenceData = new();
        private readonly List<AudioReference> _wildAudioReferenceData = new();
        private readonly AudioManagerConfig _audioManagerConfig;
        private readonly MessagePackSaveManager _saveManager;
        
        public AudioSettings AudioSettings => _audioManagerConfig.AudioSettings;
        
        #region Constructor
        [Inject]
        public AudioManager(
            AudioManagerConfig config,
            MessagePackSaveManager saveManager)
        {
            _audioManagerConfig = config;
            _saveManager = saveManager;
        }
        #endregion

        #region Life Cycle
        public void Initialize()
        {
            _audioManagerConfig.AudioSettings.BusData.Values.ForEach(busData => busData.Initialize());
            Subscribe();
        }
        
        public void PostInitialize()
        {
            Load();
        }

        public void Dispose()
        {
            Unsubscribe();
        }
        #endregion

        #region Events
        private void Subscribe()
        {
            
        }
        
        private void Unsubscribe()
        {
            
        }
        #endregion

        #region Play
        public AudioReference PlayAudio(EventReference eventReference, Vector3 position, string id = null, Transform parent = null, bool addToWildIfNotId = true)
        {
            if (_audioManagerConfig.LimitAudioCount && _wildAudioReferenceData.Count + 
                _indexedAudioReferenceData.Values.Sum(references => references.Count) >= _audioManagerConfig.MaxAudioCount)
            {
                DebugUtils.LogWarning("Max audio count reached, not playing new audio.");
                return null;
            }
            EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
            eventInstance.set3DAttributes(position.To3DAttributes());
            eventInstance.start();
            if (parent)
                RuntimeManager.AttachInstanceToGameObject(eventInstance, parent.gameObject);
            var audioReference = new AudioReference(eventInstance, eventReference, id);
            if (id != null)
            {
                if (_indexedAudioReferenceData.TryGetValue(id, out var audioReferences))
                {
                    audioReferences.Add(audioReference);
                }
                else
                {
                    _indexedAudioReferenceData[id] = new List<AudioReference> { audioReference };
                }
            }
            else if (addToWildIfNotId)
            {
                _wildAudioReferenceData.Add(audioReference);
            }
            return audioReference;
        }
        
        public void PlayAudioOneShot(EventReference eventReference, Vector3 position)
        {
            RuntimeManager.PlayOneShot(eventReference, position);
        }
        #endregion

        #region Pause/Resume
        public void SetPauseAudio(AudioReference audioReference, bool pause)
        {
            if (audioReference == null) return;
            var eventInstance = audioReference.eventInstance;
            if (!eventInstance.isValid()) return;
            eventInstance.setPaused(pause);
        }
        
        public void SetPauseAllAudioInIdentifier(string id, bool pause)
        {
            if (!_indexedAudioReferenceData.TryGetValue(id, out var audioReferences)) return;
            foreach (var audioReference in audioReferences)
            {
                SetPauseAudio(audioReference, pause);
            }
        }
        
        public void SetPauseAllIndexedAudio(bool pause)
        {
            var audioReferences = _indexedAudioReferenceData.Values
                .SelectMany(references => references);
            foreach (var audioReference in audioReferences)
            {
                SetPauseAudio(audioReference, pause);
            }
        }
        
        public void SetPauseAllWildAudio(bool pause)
        {
            foreach (var audioReference in _wildAudioReferenceData)
            {
                SetPauseAudio(audioReference, pause);
            }
        }
        
        public void SetPauseAllAudio(bool pause)
        {
            SetPauseAllIndexedAudio(pause);
            SetPauseAllWildAudio(pause);
        }

        #endregion
        
        #region Stop
        public void StopAudio(AudioReference audioReference, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT)
        {
            if (audioReference == null) return;
            var eventInstance = audioReference.eventInstance;
            if (!eventInstance.isValid()) return;
            if (audioReference.identifier != null)
            {
                var key = audioReference.identifier;
                if (_indexedAudioReferenceData.TryGetValue(key, out var audioReferences))
                {
                    audioReferences.Remove(audioReference);
                    if (audioReferences.Count == 0)
                    {
                        _indexedAudioReferenceData.Remove(key);
                    }
                }
            }
            else
            {
                _wildAudioReferenceData.Remove(audioReference);
            }
            eventInstance.stop(stopMode);
            eventInstance.release();
        }

        public void StopAllAudioInIdentifier(string id)
        {
            if (!_indexedAudioReferenceData.TryGetValue(id, out var audioReferences)) return;
            foreach (var audioReference in audioReferences)
            {
                StopAudio(audioReference);
            }
        }

        public void StopAllIndexedAudio()
        {
            var audioReferences = _indexedAudioReferenceData.Values
                .SelectMany(references => references);
            foreach (var audioReference in audioReferences)
            {
                StopAudio(audioReference);
            }
        }
        
        public void StopAllWildAudio()
        {
            foreach (var audioReference in _wildAudioReferenceData)
            {
                StopAudio(audioReference);
            }
        }
        
        public void StopAllAudio()
        {
            StopAllIndexedAudio();
            StopAllWildAudio();
        }
        #endregion

        #region Utils
        public bool TryFindAudioReference(string id, out AudioReference audioReference)
        {
            if (_indexedAudioReferenceData.TryGetValue(id, out var audioReferences))
            {
                audioReference = audioReferences.FirstOrDefault();
                return audioReference != null;
            }
            audioReference = null;
            return false;
        }
        
        public void RegisterAudioReference(AudioReference audioReference, string id)
        {
            if (id != null)
            {
                if (_indexedAudioReferenceData.TryGetValue(id, out var audioReferences))
                {
                    audioReferences.Add(audioReference);
                }
                else
                {
                    _indexedAudioReferenceData[id] = new List<AudioReference> { audioReference };
                }
            }
            else
            {
                DebugUtils.LogError("Cannot add audio reference without identifier to indexed audio references.");
            }
        }
        
        public void UnregisterAudioReference(string id)
        {
            if (id != null)
            {
                _indexedAudioReferenceData.Remove(id);
            }
            else
            {
                DebugUtils.LogError("Cannot remove audio reference without identifier from indexed audio references.");
            }
        }
        #endregion

        #region Bus
        public bool GetBusData(BusType busType, out BusData busData)
        {
            if (!_audioManagerConfig.AudioSettings.BusData.TryGetValue(busType, out busData))
            {
                DebugUtils.LogError($"Bus {busType} not found in bus dictionary.");
                return false;
            }
            if (!busData.Bus.isValid()) return false;
            return true;
        }
        
        public bool GetBusMuteState(BusType busType, out bool isMuted)
        {
            isMuted = false;
            if (!GetBusData(busType, out var busData)) return false;
            if (!busData.Bus.isValid()) return false;
            if (busData.Bus.getMute(out isMuted) is not RESULT.OK) return false;
            return true;
        }
        
        public bool GetBusVolume(BusType busType, out float volume, VolumeUnit outUnit)
        {
            volume = 0f;
            if (!GetBusData(busType, out var busData)) return false;
            if (!busData.Bus.isValid()) return false;
            if (busData.Bus.getVolume(out var linear) is not RESULT.OK) return false;
            volume = linear.ConvertUnit(VolumeUnit.Linear, outUnit);
            return true;
        }
        
        public void SetMuteBus(BusType busType, bool mute)
        {
            if (!GetBusData(busType, out var busData)) return;
            busData.SetMute(mute);
        }
        
        public void ToggleMuteBus(BusType busType)
        {
            if (!GetBusData(busType, out var busData)) return;
            busData.SetMute(!busData.IsMuted);
        }
        
        public void SetVolumeBus(BusType busType, float value, VolumeUnit inUnit)
        {
            if (!GetBusData(busType, out var busData)) return;
            busData.SetVolume(value, inUnit);
        }
        
        public void StopAllAudioInBus(BusType busType, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT)
        {
            if (!GetBusData(busType, out var busData)) return;
            if (!busData.Bus.isValid()) return;
            busData.Bus.stopAllEvents(stopMode);
            _wildAudioReferenceData.RemoveAll(audioReference => !audioReference.IsPlaying());
            foreach (var (key, audioReferences) in _indexedAudioReferenceData.ToList())
            {
                audioReferences.RemoveAll(audioReference => !audioReference.IsPlaying());
                if (audioReferences.Count == 0)
                {
                    _indexedAudioReferenceData.Remove(key);
                }
            } 
        }
        #endregion
        
        #region Save/Load

        public void Load()
        {
            Debug.Log("Loading Audio Manager Settings...");
            var audioSaveObject = _saveManager.GetFirstSaveObjectOfType<AudioSaveObject>();
            if (!audioSaveObject) return;
            var audioSaveData = audioSaveObject.GetSaveData<AudioSaveData>();
            if (audioSaveData == null) return;
            _audioManagerConfig.AudioSettings.LoadFromSaveData(audioSaveData);
        }

        public void Save()
        {
            var audioSaveObject = _saveManager.GetFirstSaveObjectOfType<AudioSaveObject>();
            if (!audioSaveObject) return;
            var audioSaveData = audioSaveObject.GetSaveData<AudioSaveData>();
            _audioManagerConfig.AudioSettings.SaveToSaveData(audioSaveData);
            _saveManager.Save(audioSaveObject);
        }
        #endregion
    }

    public static class AudioManagerUtils
    {
        /// <summary>
        /// Extension method to check if the audio reference is playing.
        /// </summary>
        /// <param name="audioReference"></param>
        /// <returns></returns>
        public static bool IsPlaying(this AudioReference audioReference)
        {
            if (audioReference == null) return false;
            if (!audioReference.eventInstance.isValid()) return false;
            var result = audioReference.eventInstance.getPlaybackState(out var state);
            if (result is not RESULT.OK) return false;
            return state != PLAYBACK_STATE.STOPPED;
        }
    }
}