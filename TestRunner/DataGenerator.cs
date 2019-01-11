﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Theraot.Collections.Specialized;
using Theraot.Collections.ThreadSafe;
using Theraot.Reflection;

namespace TestRunner
{
    public static class DataGenerator
    {
        private static readonly Dictionary<Type, SortedDictionary<Type, Delegate>> _dataGenerators = FindAllGenerators();
        private static readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        public static object Get(Type type, IEnumerable<Type> preferredTypes)
        {
            if (_dataGenerators.TryGetValue(type, out var dictionary))
            {
                Delegate @delegate = null;
                foreach (var preferredType in preferredTypes)
                {
                    if (!dictionary.TryGetValue(preferredType, out var found))
                    {
                        continue;
                    }
                    @delegate = found;
                    break;
                }
                if (@delegate == null)
                {
                    @delegate = dictionary.First().Value;
                }
                return @delegate.DynamicInvoke(ArrayReservoir<object>.EmptyArray);
            }
            return type.GetTypeInfo().IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static Dictionary<Type, SortedDictionary<Type, Delegate>> FindAllGenerators()
        {
            var result = new Dictionary<Type, SortedDictionary<Type, Delegate>>();
            var generators = TypeDiscoverer.GetAllTypes()
                .SelectMany(t => t.GetTypeInfo().GetMethods())
                .Where(IsGeneratorMethod)
                .Select(GetGenerators);
            var typeComparer = new CustomComparer<Type>
                (
                    (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal)
                );
            foreach (var generator in generators)
            {
                var type = generator.ReturnType;
                var @delegate = generator.Delegate;
                if (@delegate == null)
                {
                    continue;
                }
                if (result.TryGetValue(type, out var dictionary))
                {
                    dictionary.TryAdd(generator.GeneratorType, @delegate);
                }
                else
                {
                    dictionary = new SortedDictionary<Type, Delegate>(typeComparer)
                    {
                        {generator.GeneratorType, @delegate}
                    };
                    result.Add(type, dictionary);
                }
            }
            return result;
        }

        private static (Type ReturnType, Type GeneratorType, Delegate Delegate) GetGenerators(MethodInfo methodInfo)
        {
            Delegate @delegate;
            if (methodInfo.IsStatic)
            {
                @delegate = TypeHelper.BuildDelegate(methodInfo, null);
            }
            else
            {
                var declaringType = methodInfo.DeclaringType;
                if (declaringType == null)
                {
                    throw new InvalidOperationException();
                }
                if (!_instances.TryGetValue(declaringType, out var instance))
                {
                    try
                    {
                        instance = Activator.CreateInstance(declaringType);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                    }
                    _instances[declaringType] = instance;
                }
                if (instance == null)
                {
                    @delegate = null;
                }
                else
                {
                    @delegate = TypeHelper.BuildDelegate(methodInfo, instance);
                }
            }
            return (methodInfo.GetReturnType(), methodInfo.DeclaringType, @delegate);
        }

        private static bool IsGeneratorMethod(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute<DataGeneratorAttribute>() && methodInfo.DeclaringType != null &&
                   methodInfo.GetParameters().Length == 0;
        }
    }

    public static class NumericGenerator
    {
        private static readonly Random _random = new Random();

        [DataGenerator]
        public static int GenerateInt()
        {
            var buffer = new byte[4];
            _random.NextBytes(buffer);
            return BitConverter.ToInt32(buffer, 0);
        }
    }

    public static class SmallPositiveNumericGenerator
    {
        private static readonly Random _random = new Random();

        [DataGenerator]
        public static byte GenerateByte()
        {
            var buffer = new byte[1];
            _random.NextBytes(buffer);
            return buffer[0];
        }

        [DataGenerator]
        public static int GenerateInt()
        {
            return _random.Next(0, 2000);
        }
    }

    public static class StringGenerator
    {
        private static readonly char[] _chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
        private static readonly Random _random = new Random();

        [DataGenerator]
        public static string GenerateString()
        {
            var length = _random.Next(1, 16);
            var stringBuilder = new StringBuilder(length);
            for (var index = 0; index < length; index++)
            {
                stringBuilder.Append(_chars[_random.Next(0, _chars.Length)]);
            }
            return stringBuilder.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DataGeneratorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public sealed class UseGeneratorAttribute : Attribute
    {
        public UseGeneratorAttribute(Type type)
        {
            GeneratorType = type;
        }

        public Type GeneratorType { get; }
    }
}