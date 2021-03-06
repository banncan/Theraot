﻿#if LESSTHAN_NET35
// ReSharper disable LoopCanBeConvertedToQuery

using System.Collections;
using System.Collections.Generic;
using Theraot.Collections.Specialized;
using Theraot.Core;

namespace System.Linq
{
    public static partial class Enumerable
    {
        public static IEnumerable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return GroupBy(source, keySelector, null);
        }

        public static IEnumerable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TSource>(source, keySelector, FuncHelper.GetIdentityFunc<TSource>(), comparer);
        }

        public static IEnumerable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)
        {
            return GroupBy(source, keySelector, elementSelector, null);
        }

        public static IEnumerable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }
            if (elementSelector == null)
            {
                throw new ArgumentNullException(nameof(elementSelector));
            }
            return CreateGroupByIterator(source, keySelector, elementSelector, comparer);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector)
        {
            return GroupBy(source, keySelector, elementSelector, resultSelector, null);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }
            if (elementSelector == null)
            {
                throw new ArgumentNullException(nameof(elementSelector));
            }
            return CreateGroupByIterator(source, keySelector, elementSelector, resultSelector, comparer);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector)
        {
            return GroupBy(source, keySelector, resultSelector, null);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TSource, TResult>(source, keySelector, FuncHelper.GetIdentityFunc<TSource>(), resultSelector, comparer);
        }

        private static IEnumerable<IGrouping<TKey, TElement>> CreateGroupByIterator<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TElement>(source, keySelector, elementSelector, comparer)/* as IEnumerable<IGrouping<TKey, TElement>>*/;
        }

        private static IEnumerable<TResult> CreateGroupByIterator<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TElement, TResult>(source, keySelector, elementSelector, resultSelector, comparer)/* as IEnumerable<TResult>*/;
        }

        internal class GroupedEnumerable<TSource, TKey, TElement, TResult> : IEnumerable<TResult>
        {
            private readonly IEqualityComparer<TKey> _comparer;
            private readonly Func<TSource, TElement> _elementSelector;
            private readonly Func<TSource, TKey> _keySelector;
            private readonly Func<TKey, IEnumerable<TElement>, TResult> _resultSelector;
            private readonly IEnumerable<TSource> _source;

            public GroupedEnumerable(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
                _elementSelector = elementSelector ?? throw new ArgumentNullException(nameof(elementSelector));
                _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
                _comparer = comparer;
            }

            public IEnumerator<TResult> GetEnumerator()
            {
                var groupings = new NullAwareDictionary<TKey, Tuple<Grouping<TKey, TElement>, List<TElement>>>(_comparer);
                foreach (var item in _source)
                {
                    var key = _keySelector(item);
                    if (!groupings.TryGetValue(key, out var tuple))
                    {
                        var collection = new List<TElement>();
                        tuple = new Tuple<Grouping<TKey, TElement>, List<TElement>>(new Grouping<TKey, TElement>(key, collection), collection);
                        groupings.Add(key, tuple);
                    }
                    tuple.Item2.Add(_elementSelector(item));
                }
                return Enumerator(groupings);
            }

            private IEnumerator<TResult> Enumerator(IDictionary<TKey, Tuple<Grouping<TKey, TElement>, List<TElement>>> groupings)
            {
                foreach (var (grouping, _) in groupings.Values)
                {
                    yield return _resultSelector(grouping.Key, grouping);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal class GroupedEnumerable<TSource, TKey, TElement> : IEnumerable<IGrouping<TKey, TElement>>
        {
            private readonly IEqualityComparer<TKey> _comparer;
            private readonly Func<TSource, TElement> _elementSelector;
            private readonly Func<TSource, TKey> _keySelector;
            private readonly IEnumerable<TSource> _source;

            public GroupedEnumerable(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
                _elementSelector = elementSelector ?? throw new ArgumentNullException(nameof(elementSelector));
                _comparer = comparer;
            }

            public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
            {
                var groupings = new NullAwareDictionary<TKey, Tuple<Grouping<TKey, TElement>, List<TElement>>>(_comparer);
                foreach (var item in _source)
                {
                    var key = _keySelector(item);
                    if (!groupings.TryGetValue(key, out var tuple))
                    {
                        var collection = new List<TElement>();
                        tuple = new Tuple<Grouping<TKey, TElement>, List<TElement>>(new Grouping<TKey, TElement>(key, collection), collection);
                        groupings.Add(key, tuple);
                    }
                    tuple.Item2.Add(_elementSelector(item));
                }
                return Enumerator(groupings);
            }

            private static IEnumerator<IGrouping<TKey, TElement>> Enumerator(IDictionary<TKey, Tuple<Grouping<TKey, TElement>, List<TElement>>> groupings)
            {
                foreach (var grouping in groupings.Values)
                {
                    yield return grouping.Item1;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}

#endif