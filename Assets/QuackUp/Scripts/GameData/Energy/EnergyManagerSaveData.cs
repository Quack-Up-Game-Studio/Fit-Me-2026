using System;
using System.Collections.Generic;
using MessagePack;
using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [MessagePackObject]
    [Serializable]
    public class EnergyManagerSaveData : IMessagePackSaveData
    {
        [Key("Version")] 
        [field: SerializeField] public string Version { get; set; } = string.Empty;
        
        [Key("CurrentEnergy")]
        [field: SerializeField] public int CurrentEnergy { get; set; } = 0;
        
        [Key("LastUpdateTime")]
        public DateTime LastEnergyUpdateTime { get; set; } = DateTime.MinValue;

        [Key("AppliedPurchaseTransactionIds")]
        public List<string> AppliedPurchaseTransactionIds { get; set; } = new();

        public bool TryApplyPurchaseGrant(string transactionId, int amount)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                throw new ArgumentException("A purchase transaction ID is required.", nameof(transactionId));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Purchase energy grant must be positive.");

            AppliedPurchaseTransactionIds ??= new List<string>();
            if (AppliedPurchaseTransactionIds.Contains(transactionId)) return false;

            CurrentEnergy = (int)Math.Min(int.MaxValue, (long)CurrentEnergy + amount);
            AppliedPurchaseTransactionIds.Add(transactionId);
            return true;
        }
        
        [IgnoreMember]
        [ShowInInspector] private string DebugLastEnergyUpdateTime => LastEnergyUpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}