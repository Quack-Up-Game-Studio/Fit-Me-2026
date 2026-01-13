using System;
using System.Linq;

namespace QuackUp.Utils
{
    public static class EnumUtils
    {
        /// <summary>
        /// Get the maximum value of an enum type.
        /// </summary>
        /// <typeparam name="TEnum"></typeparam>
        /// <returns></returns>
        public static TEnum Max<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Max();
        }

        /// <summary>
        /// Get the minimum value of an enum type.
        /// </summary>
        /// <typeparam name="TEnum"></typeparam>
        /// <returns></returns>
        public static TEnum Min<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Min();
        }
        
        /// <summary>
        /// Randomly select a value from an enum type.
        /// </summary>
        /// <typeparam name="TEnum"></typeparam>
        /// <returns></returns>
        public static TEnum RandomValue<TEnum>() where TEnum : Enum
        {
            var values = Enum.GetValues(typeof(TEnum));
            return (TEnum)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        }
    }
}