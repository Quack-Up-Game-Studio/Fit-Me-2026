using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UniLabs.Time;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "EnergyManagerConfig", menuName = "FitMe/GameData/Energy/EnergyManagerConfig")]
    public class EnergyManagerConfig : SerializedScriptableObject
    {
        [field: SerializeField] public int MaxEnergy { get; private set; } = 5;
        [field: TimeSpanDrawerSettings(TimeUnit.Minutes), SerializeField]
        public UTimeSpan EnergyRechargeTime { get; private set; } = TimeSpan.FromSeconds(10f);
    }
}