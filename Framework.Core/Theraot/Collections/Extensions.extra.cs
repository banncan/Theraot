﻿#if FAT
using System;
using System.Collections.Generic;
using Theraot.Collections.Specialized;

namespace Theraot.Collections
{
    public static partial class Extensions
    {
        public static IEnumerable<T> Append<T>(this IEnumerable<T> target, IEnumerable<T> append)
        {
            return new ExtendedEnumerable<T>(target, append);
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> target, T append)
        {
            return new ExtendedEnumerable<T>(target, AsUnaryIEnumerable(append));
        }

        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> target, IEnumerable<T> prepend)
        {
            return new ExtendedEnumerable<T>(prepend, target);
        }

        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> target, T prepend)
        {
            return new ExtendedEnumerable<T>(AsUnaryIEnumerable(prepend), target);
        }

        public static IEnumerable<T> Cycle<T>(this IEnumerable<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var progressive = AsICollection(source);
            return CycleExtracted();

            IEnumerable<T> CycleExtracted()
            {
                while (true)
                {
                    foreach (var item in progressive)
                    {
                        yield return item;
                    }
                }
                // Infinite Loop - This method creates an endless IEnumerable<T>
                // ReSharper disable once IteratorNeverReturns
            }
        }
    }
}

#endif