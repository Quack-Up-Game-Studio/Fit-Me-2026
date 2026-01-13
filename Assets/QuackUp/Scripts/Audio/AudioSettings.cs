using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using QuackUp.Utils;
using MessagePack;
using MessagePack.Formatters;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.Audio
{
    [MessagePackFormatter(typeof(EnumAsStringFormatter<BusType>))]
    public enum BusType
    {
        Master,
        BGM,
        SFX,
    }
    
    public enum VolumeUnit
    {
        Linear,
        Linear01,
        Decibel,
        Decibel01
    }
    
    [Serializable]
    public record BusData(string Path, bool IsMaster = false, float LinearVolume = 1f, bool IsMuted = false)
    {
        [field: SerializeField, DisableInPlayMode] public string Path { get; private set; } = Path;
        
        [field: SerializeField, DisableInPlayMode] public bool IsMaster { get; private set; } = IsMaster;

        [field: SerializeField, PropertyRange(AudioSettingsUtils.MinLinearVolume, AudioSettingsUtils.MaxLinearVolume), 
                OnValueChanged(nameof(LinearChanged))]
        public float LinearVolume { get; private set; } = LinearVolume;
        
        [field: SerializeField, PropertyRange(0f, 1f),
                OnValueChanged(nameof(Linear01Changed))]
        public float Linear01Volume { get; private set; }

        [field: SerializeField, PropertyRange(AudioSettingsUtils.MinDecibelVolume, AudioSettingsUtils.MaxDecibelVolume), 
                OnValueChanged(nameof(DecibelChanged))]
        public float DecibelVolume { get; private set; }
        
        [field: SerializeField, PropertyRange(0f, 1f),
                OnValueChanged(nameof(Decibel01Changed))]
        public float Decibel01Volume { get; private set; }
        [field: SerializeField, OnValueChanged(nameof(MuteChanged))] public bool IsMuted { get; private set; } = IsMuted;
        
        public Bus Bus { get; private set; }

        public void Initialize()
        {
            var bus = RuntimeManager.GetBus(Path);
            if (!bus.isValid())
            {
                DebugUtils.LogError($"Bus with path {Path} not found. Please check the FMOD settings.");
                return;
            }
            Bus = bus;
            SetVolume(LinearVolume);
            LinearChanged();
            SetMute(IsMuted);
        }
        
        public void SetMute(bool mute)
        {
            if (!Bus.isValid()) return;
            if (IsMaster)
                RuntimeManager.MuteAllEvents(mute);
            else
            {
                Bus.setMute(mute);
            }
                
            IsMuted = mute;
        }

        public void SetVolume(float volume, VolumeUnit unit = VolumeUnit.Linear)
        {
            if (!Bus.isValid()) return;
            switch (unit)
            {
                case VolumeUnit.Linear:
                    LinearVolume = volume;
                    LinearChanged();
                    break;
                case VolumeUnit.Decibel:
                    DecibelVolume = volume;
                    DecibelChanged();
                    break;
                case VolumeUnit.Linear01:
                    Linear01Volume = volume;
                    Linear01Changed();
                    break;
                case VolumeUnit.Decibel01:
                    Decibel01Volume = volume;
                    Decibel01Changed();
                    break;
            }
            var linear = volume.ConvertUnit(unit, VolumeUnit.Linear);
            Debug.Log("Linear volume set: " + volume);
            SetVolumeLinear(linear);
        }

        private void SetVolumeLinear(float linear)
        {
            if (!Bus.isValid()) return;
            Bus.setVolume(linear);
        }
        private void LinearChanged()
        {
            DecibelVolume = LinearVolume.ConvertUnit(VolumeUnit.Linear, VolumeUnit.Decibel);
            Linear01Volume = LinearVolume.ConvertUnit(VolumeUnit.Linear, VolumeUnit.Linear01);
            Decibel01Volume = LinearVolume.ConvertUnit(VolumeUnit.Linear, VolumeUnit.Decibel01);
            SetVolumeLinear(LinearVolume);
        }
        
        private void DecibelChanged()
        {
            LinearVolume = DecibelVolume.ConvertUnit(VolumeUnit.Decibel, VolumeUnit.Linear);
            Linear01Volume = DecibelVolume.ConvertUnit(VolumeUnit.Decibel, VolumeUnit.Linear01);
            Decibel01Volume = DecibelVolume.ConvertUnit(VolumeUnit.Decibel, VolumeUnit.Decibel01);
            SetVolumeLinear(LinearVolume);
        }
        
        private void Linear01Changed()
        {
            LinearVolume = Linear01Volume.ConvertUnit(VolumeUnit.Linear01, VolumeUnit.Linear);
            DecibelVolume = Linear01Volume.ConvertUnit(VolumeUnit.Linear01, VolumeUnit.Decibel);
            Decibel01Volume = Linear01Volume.ConvertUnit(VolumeUnit.Linear01, VolumeUnit.Decibel01);
            SetVolumeLinear(LinearVolume);
        }
        
        private void Decibel01Changed()
        {
            DecibelVolume = Decibel01Volume.ConvertUnit(VolumeUnit.Decibel01, VolumeUnit.Decibel);
            LinearVolume = Decibel01Volume.ConvertUnit(VolumeUnit.Decibel01, VolumeUnit.Linear);
            Linear01Volume = Decibel01Volume.ConvertUnit(VolumeUnit.Decibel01, VolumeUnit.Linear01);
            SetVolumeLinear(LinearVolume);
        }
        
        private void MuteChanged()
        {
            SetMute(IsMuted);
        }
    }

    [CreateAssetMenu(fileName = "AudioSettings", menuName = "QuackUp/Audio/AudioSettings", order = 1)]
    [ShowOdinSerializedPropertiesInInspector]
    public class AudioSettings : SerializedScriptableObject
    {
        [HideReferenceObjectPicker]
        [field: OdinSerialize] public Dictionary<BusType, BusData> BusData { get; private set; } = new()
        {
            { BusType.Master, new BusData("bus:/", true) },
            { BusType.BGM, new BusData("bus:/BGM") },
            { BusType.SFX, new BusData("bus:/SFX") },
        };
        
        public void LoadFromSaveData(AudioSaveData saveData)
        {
            Debug.Log("Loading audio settings from save data.");
            foreach (var busEntry in BusData)
            {
                if (!saveData.BusSaveData.TryGetValue(busEntry.Key, out var busSaveData)) continue;
                busEntry.Value.SetVolume(busSaveData.LinearVolume);
                busEntry.Value.SetMute(busSaveData.IsMuted);
            }
        }
        
        public void SaveToSaveData(AudioSaveData saveData)
        {
            saveData ??= new AudioSaveData();
            foreach (var busEntry in BusData)
            {
                if (!saveData.BusSaveData.ContainsKey(busEntry.Key))
                {
                    saveData.BusSaveData[busEntry.Key] = new BusSaveData();
                }
                var busSaveData = saveData.BusSaveData[busEntry.Key];
                busSaveData.LinearVolume = busEntry.Value.LinearVolume;
                busSaveData.IsMuted = busEntry.Value.IsMuted;
            }
        }
    }

    public static class AudioSettingsUtils
    {
        public const float MinLinearVolume = 0f;
        public const float MaxLinearVolume = 3.16227766f;
        public const float MinDecibelVolume = -80f;
        public const float MaxDecibelVolume = 10f;
        
        private static float Linear01ToDb(float lin01)
        {
            float linear = lin01 * MaxLinearVolume;
            return Mathf.Clamp(Mathf.Log10(Mathf.Max(linear, 1e-10f)) * 20f, MinDecibelVolume, MaxDecibelVolume);
        }
        
        private static float Db01ToLinear(float db01)
        {
            float db = Mathf.Lerp(MinDecibelVolume, MaxDecibelVolume, db01);
            return Mathf.Clamp(Mathf.Pow(10f, db / 20f), MinLinearVolume, MaxLinearVolume);
        }
        
        /// <summary>
        /// Converts a volume value from one unit to another.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="inUnit"></param>
        /// <param name="outUnit"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static float ConvertUnit(this float value, VolumeUnit inUnit, VolumeUnit outUnit)
        {
            if (inUnit == outUnit) return value;

            bool linear = inUnit is VolumeUnit.Linear or VolumeUnit.Linear01;
            float range01 = inUnit switch
            {
                VolumeUnit.Linear   => Mathf.InverseLerp(MinLinearVolume, MaxLinearVolume, value),
                VolumeUnit.Linear01 => Mathf.Clamp01(value),
                VolumeUnit.Decibel  => Mathf.InverseLerp(MinDecibelVolume, MaxDecibelVolume, value),
                VolumeUnit.Decibel01=> Mathf.Clamp01(value),
                _ => throw new ArgumentOutOfRangeException(nameof(inUnit))
            };
            
            if (linear)
            {
                return outUnit switch
                {
                    VolumeUnit.Linear => Mathf.Lerp(MinLinearVolume, MaxLinearVolume, range01),
                    VolumeUnit.Linear01 => range01,
                    VolumeUnit.Decibel  => Linear01ToDb(range01),
                    VolumeUnit.Decibel01=> Mathf.InverseLerp(MinDecibelVolume, MaxDecibelVolume, Linear01ToDb(range01)),
                    _ => throw new ArgumentOutOfRangeException(nameof(outUnit))
                };
            }
            return outUnit switch
            {
                VolumeUnit.Linear => Db01ToLinear(range01),
                VolumeUnit.Linear01 => Mathf.InverseLerp(MinLinearVolume, MaxLinearVolume, Db01ToLinear(range01)),
                VolumeUnit.Decibel  => Mathf.Lerp(MinDecibelVolume, MaxDecibelVolume, range01),
                VolumeUnit.Decibel01=> range01,
                _ => throw new ArgumentOutOfRangeException(nameof(outUnit))
            };
        }
    }
}