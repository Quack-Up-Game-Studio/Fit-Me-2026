#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using R3;

namespace QuackUp.Utils
{
    /// <summary>
    /// Serialize only the value of ReactiveProperty, not the subscription information.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReactivePropertyValueFormatter<T> : IMessagePackFormatter<ReactiveProperty<T>?>
    {
        public void Serialize(ref MessagePackWriter writer, ReactiveProperty<T>? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.Value, options);
            }
        }

        public ReactiveProperty<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }
            var value = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
            return new ReactiveProperty<T>(value);
        }
    }
    
    /// <summary>
    /// Serialize only the value of ReadOnlyReactiveProperty, not the subscription information.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReadOnlyReactivePropertyValueFormatter<T> : IMessagePackFormatter<ReadOnlyReactiveProperty<T>?>
    {
        public void Serialize(ref MessagePackWriter writer, ReadOnlyReactiveProperty<T>? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.CurrentValue, options);
            }
        }

        public ReadOnlyReactiveProperty<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }
            var value = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
            return new ReactiveProperty<T>(value);
        }
    }
    
    public class ReactivePropertyValueResolver : IFormatterResolver
    {
        public static readonly ReactivePropertyValueResolver Instance = new();

        private ReactivePropertyValueResolver()
        {
        }

        public IMessagePackFormatter<T>? GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            internal static readonly IMessagePackFormatter<T>? Formatter;

            static FormatterCache()
            {
                Formatter = (IMessagePackFormatter<T>?)ReactivePropertyResolverGetFormatterHelper.GetFormatter(typeof(T));
            }
        }
    }

    internal static class ReactivePropertyResolverGetFormatterHelper
    {
        private static readonly Dictionary<Type, Type> FormatterMap = new()
        {
              { typeof(ReactiveProperty<>), typeof(ReactivePropertyValueFormatter<>) },
              { typeof(ReadOnlyReactiveProperty<>), typeof(ReadOnlyReactivePropertyValueFormatter<>) },
        };

        internal static object? GetFormatter(Type t)
        {
            var ti = t.GetTypeInfo();
            if (!ti.IsGenericType) return null;
            var genericType = ti.GetGenericTypeDefinition();
            return FormatterMap.TryGetValue(genericType, out var formatterType) 
                ? CreateInstance(formatterType, ti.GenericTypeArguments) 
                : null;
        }

        private static object? CreateInstance(Type genericType, Type[] genericTypeArguments, params object[] arguments)
        {
            return Activator.CreateInstance(genericType.MakeGenericType(genericTypeArguments), arguments);
        }
    }
}