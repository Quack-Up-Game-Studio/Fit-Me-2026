using System;
using System.Globalization;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace QuackUp.Utils
{
    /// <summary>
    /// Used as a placeholder in the inspector when no value is needed.
    /// </summary>
    [Serializable]
    public struct InspectorPlaceholder
    {
    }
    
    /// <summary>
    /// Represents a percentage value that can be converted to different formats.
    /// </summary>
    [Serializable]
    public struct Percentage : IEquatable<Percentage>, IComparable<Percentage>, IFormattable
    {
        [HideLabel, Unit(Units.Percent),
         SerializeField] private float value;
        /// <summary>
        /// Returns the raw percentage value (e.g., 50% -> 50, -50% -> -50).
        /// </summary>
        public float AsPercentage => value;
        /// <summary>
        /// Returns the percentage as a multiplier (e.g., 50% -> 1.5, -50% -> 0.5).
        /// </summary>
        public float AsMultiplier => 1f + value / 100f;
        /// <summary>
        /// Return percentage as a fraction (e.g., 50% -> 0.5, -150% -> -1.5).
        /// </summary>
        public float AsFraction => value / 100f;
        /// <summary>
        /// Returns the percentage as an inverse fraction (e.g., 75% -> 0.25, 150% -> -0.5).
        /// </summary>
        public float AsInverseFraction => 1f - value / 100f;
        /// <summary>
        /// Returns the percentage as an inverse percentage (e.g., 75% -> 25%, 150% -> -50%).
        /// </summary>
        public Percentage AsInversePercentage => new(100f - value);
        /// <summary>
        /// 0%
        /// </summary>
        public static Percentage Zero => new(0f);
        /// <summary>
        /// 25%
        /// </summary>
        public static Percentage Quarter => new(25f);
        /// <summary>
        /// 50%
        /// </summary>
        public static Percentage Half => new(50f);
        /// <summary>
        /// 75%
        /// </summary>
        public static Percentage ThreeQuarters => new(75f);
        /// <summary>
        /// 100%
        /// </summary>
        public static Percentage Full => new(100f);

        private Percentage(float percentage)
        {
            value = percentage;
        }
        
        // Factory methods
        /// <summary>
        /// Creates a Percentage from a percentage value.
        /// </summary>
        /// <param name="percentage">Example: 50 for 50%. -50 for -50%.</param>
        /// <returns></returns>
        public static Percentage FromPercentage(float percentage) => new(percentage);
        /// <summary>
        /// Creates a Percentage from a fraction value.
        /// </summary>
        /// <param name="fraction">Example: 0.5 for +50%. -0.5 for -50%.</param>
        /// <returns></returns>
        public static Percentage FromFraction(float fraction) => new(fraction * 100f);
        /// <summary>
        /// Creates a Percentage from a multiplier value. 
        /// </summary>
        /// <param name="multiplier">Example: 1.5 for +50%. -1.5 for -50%.</param>
        /// <returns></returns>
        public static Percentage FromMultiplier(float multiplier) => new((multiplier - 1f) * 100f);
        
        // Comparison implementation
        public bool Equals(Percentage other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is Percentage other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public int CompareTo(Percentage other) => value.CompareTo(other.value);
        public static bool operator ==(Percentage left, Percentage right) => left.Equals(right);
        public static bool operator !=(Percentage left, Percentage right) => !left.Equals(right);
        public static bool operator <(Percentage left, Percentage right) => left.CompareTo(right) < 0;
        public static bool operator >(Percentage left, Percentage right) => left.CompareTo(right) > 0;
        public static bool operator <=(Percentage left, Percentage right) => left.CompareTo(right) <= 0;
        public static bool operator >=(Percentage left, Percentage right) => left.CompareTo(right) >= 0;
        
        // Math operations
        public static Percentage operator +(Percentage a, Percentage b) => new(a.value + b.value);
        public static Percentage operator -(Percentage a, Percentage b) => new(a.value - b.value);
        public static Percentage operator *(Percentage a, Percentage b) => new(a.AsFraction * b.AsFraction * 100f);
        public static Percentage operator /(Percentage a, Percentage b) => new(a.AsFraction / b.AsFraction * 100f);

        // String formatting
        public override string ToString() => ToPercentageString(); // Default to percentage format
        public string ToString(string format, IFormatProvider formatProvider) => value.ToString(format, formatProvider);
        
        public string ToPercentageString(string format = "F2", string suffix = "%", IFormatProvider formatProvider = null)
        {
            return AsPercentage.ToString(format, formatProvider ?? CultureInfo.InvariantCulture) + suffix;
        }
        
        public string ToMultiplierString(string format = "F2", string suffix = "x", IFormatProvider formatProvider = null)
        {
            return AsMultiplier.ToString(format, formatProvider ?? CultureInfo.InvariantCulture) + suffix;
        }
        
        public string ToFractionString(string format = "F2", IFormatProvider formatProvider = null)
        {
            return AsFraction.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
        }
        
        //Clamping
        public static Percentage Clamp(Percentage percentage, float min, float max)
        {
            return new Percentage(Mathf.Clamp(percentage.AsPercentage, min, max));
        }
        
        public static Percentage Clamp01(Percentage percentage)
        {
            return FromFraction(Mathf.Clamp01(percentage.AsFraction));
        }
        
        //Chance
        /// <summary>
        /// Returns true with the given percentage chance.
        /// </summary>
        /// <param name="percentage"></param>
        /// <returns></returns>
        public static bool TryRoll(Percentage percentage)
        {
            return percentage.AsFraction switch
            {
                <= 0 => false,
                >= 1 => true,
                _ => Random.value <= percentage.AsFraction
            };
        }
    }

    /// <summary>
    /// Unsigned float, always >= 0.
    /// </summary>
    [Serializable]
    public struct UFloat : IEquatable<UFloat>, IComparable<UFloat>, IFormattable
    {
        [MinValue(0),
         ValidateInput(nameof(ValidateInput), "Value cannot be negative"),
         HideLabel,
         SerializeField] private float value;

        public float Value => value;

        private bool ValidateInput(ref float newValue)
        {
            if (newValue < 0)
            {
                //DebugUtils.LogWarning("UFloat value cannot be negative. Setting to 0.");
                newValue = 0;
                return false;
            }

            value = Mathf.Max(0, newValue);
            return true;
        }

        public UFloat(float value)
        {
            this.value = 0;
            ValidateInput(ref value);
        }

        // Implicit conversions
        public static implicit operator float(UFloat uf) => uf.Value;
        public static implicit operator UFloat(float f) => new(f);

        // Equality implementation
        public bool Equals(UFloat other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is UFloat other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();

        // Comparison implementation
        public int CompareTo(UFloat other) => value.CompareTo(other.value);

        // Operators
        public static bool operator ==(UFloat left, UFloat right) => left.Equals(right);
        public static bool operator !=(UFloat left, UFloat right) => !left.Equals(right);
        public static bool operator <(UFloat left, UFloat right) => left.CompareTo(right) < 0;
        public static bool operator >(UFloat left, UFloat right) => left.CompareTo(right) > 0;
        public static bool operator <=(UFloat left, UFloat right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UFloat left, UFloat right) => left.CompareTo(right) >= 0;

        // Math operations
        public static UFloat operator +(UFloat a, UFloat b) => new(a.value + b.value);
        public static UFloat operator -(UFloat a, UFloat b) => new(a.value - b.value);
        public static UFloat operator *(UFloat a, UFloat b) => new(a.value * b.value);
        public static UFloat operator /(UFloat a, UFloat b) => new(a.value / b.value);

        // String formatting
        public override string ToString() => value.ToString(CultureInfo.InvariantCulture);
        public string ToString(string format, IFormatProvider formatProvider) => value.ToString(format, formatProvider);
    }

    /// <summary>
    /// Struct to define insets for a RectTransform.
    /// </summary>
    [Serializable]
    public struct RectTransformInset
    {
        [HorizontalGroup("LeftTop")] public float left;
        [HorizontalGroup("LeftTop")] public float top;
        [HorizontalGroup("RightBottom")] public float right;
        [HorizontalGroup("RightBottom")] public float bottom;
        public Vector2 OffsetMin => new(left, bottom);
        public Vector2 OffsetMax => new(-right, -top);
    }
}