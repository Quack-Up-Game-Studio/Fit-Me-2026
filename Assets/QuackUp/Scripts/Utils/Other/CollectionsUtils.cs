using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace QuackUp.Utils
{
    public class OrderedDictionary<TKey, TValue> : OrderedDictionary, IReadOnlyOrderedDictionary<TKey, TValue>
    {
        public bool ContainsKey(TKey key)
        {
            return Contains(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (Contains(key))
            {
                value = (TValue)base[key];
                return true;
            }
            value = default;
            return false;
        }

        public TValue this[TKey key]
        {
            get => (TValue)base[key];
            set => base[key] = value;
        }

        public new TValue this[int index]
        {
            get => (TValue)base[index];
            set => base[index] = value;
        }

        public new IEnumerable<TKey> Keys => base.Keys.Cast<TKey>();
        public new IEnumerable<TValue> Values => base.Values.Cast<TValue>();
        
        public bool TryAdd(TKey key, TValue value)
        {
            if (Contains(key))
                return false;
            base.Add(key, value);
            return true;
        }
        
        /// <remarks>
        /// Use <see cref="Add(TKey key, TValue value)"/> instead. This method is not type-safe and will throw an exception if the key or value is of the wrong type.
        /// </remarks>
        [Obsolete("Use Add(TKey key, TValue value) instead.")]
        public new void Add(object key, object value)
        {
            Add((TKey)key, (TValue)value);
        }
        
        public void Add(TKey key, TValue value)
        {
            base.Add(key, value);
        }

        public new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var entry in this)
            {
                yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }
    }
    
    public interface IReadOnlyOrderedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        new TValue this[TKey key] { get; }
        new TValue this[int index] { get; }
    }
}