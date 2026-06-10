using System;
using System.Collections.Generic;

namespace QuackUp.IAP
{
    public static class IapCurrencyHelper
    {
        // ISO 4217 zero-decimal currencies (fractional digits = 0)
        private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
        {
            "BIF", // Burundian Franc
            "CLP", // Chilean Peso
            "DJF", // Djiboutian Franc
            "GNF", // Guinean Franc
            "HUF", // Hungarian Forint
            "ISK", // Icelandic Króna
            "JPY", // Japanese Yen
            "KMF", // Comorian Franc
            "KRW", // South Korean Won
            "LAK", // Lao Kip
            "MGA", // Malagasy Ariary
            "PYG", // Paraguayan Guaraní
            "RWF", // Rwandan Franc
            "UGX", // Ugandan Shilling
            "VND", // Vietnamese Dong
            "VUV", // Vanuatu Vatu
            "XAF", // CFA Franc BEAC
            "XOF", // CFA Franc BCEAO
            "XPF"  // CFP Franc
        };

        /// <summary>
        /// Converts the localized decimal price from Unity IAP to the lowest denomination (minor units / cents) 
        /// required by GameAnalytics, taking zero-decimal currencies into account.
        /// </summary>
        public static int GetAmountInMinorUnits(decimal localizedPrice, string isoCurrencyCode)
        {
            if (string.IsNullOrEmpty(isoCurrencyCode))
            {
                return decimal.ToInt32(Math.Round(localizedPrice * 100));
            }

            if (ZeroDecimalCurrencies.Contains(isoCurrencyCode))
            {
                // JPY, KRW, etc. use the whole number value directly
                return decimal.ToInt32(Math.Round(localizedPrice));
            }

            // Standard currencies (USD, EUR, GBP) convert to cents
            return decimal.ToInt32(Math.Round(localizedPrice * 100));
        }
    }
}
