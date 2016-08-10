﻿// Needed for NET40

using System;
using System.Collections.Generic;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    /// <summary>
    /// Represent a thread-safe wait-free fixed size bucket.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <remarks>
    /// Consider wrapping this class to implement <see cref="ICollection{T}" /> or any other desired interface.
    /// </remarks>
    [Serializable]
    public sealed class Bucket<T> : IEnumerable<T>
    {
        private readonly int _capacity;
        private int _count;
        private object[] _entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bucket{T}" /> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public Bucket(int capacity)
        {
            _count = 0;
            _capacity = Grow(capacity);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bucket{T}" /> class.
        /// </summary>
        public Bucket(IEnumerable<T> source)
        {
            var collection = source as ICollection<T>;
            _capacity = Grow(collection == null ? 64 : collection.Count);
            foreach (var item in source)
            {
                if (_count == _capacity)
                {
                    _capacity = Grow(_capacity << 1);
                }
#pragma warning disable 618 // Use only during initialization
                SetPrivate(_count, item);
#pragma warning restore 618
                _count++;
            }
        }

        ~Bucket()
        {
            if (!AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
                RecyclePrivate();
            }
        }

        /// <summary>
        /// Gets the capacity.
        /// </summary>
        public int Capacity
        {
            get
            {
                return _capacity;
            }
        }

        /// <summary>
        /// Gets the number of items actually contained.
        /// </summary>
        public int Count
        {
            get
            {
                return _count;
            }
        }

        /// <summary>
        /// Copies the items to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="arrayIndex">Index of the array.</param>
        /// <exception cref="System.ArgumentNullException">array</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">arrayIndex;Non-negative number is required.</exception>
        /// <exception cref="System.ArgumentException">array;The array can not contain the number of elements.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException("arrayIndex", "Non-negative number is required.");
            }
            if (_count > array.Length - arrayIndex)
            {
                throw new ArgumentException("The array can not contain the number of elements.", "array");
            }
            try
            {
                CopyToPrivate(array, arrayIndex);
            }
            catch (IndexOutOfRangeException exception)
            {
                throw new ArgumentOutOfRangeException("array", exception.Message);
            }
        }

        /// <summary>
        /// Sets the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <param name="previous">The previous item in the specified index.</param>
        /// <returns>
        ///   <c>true</c> if the item was new; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public bool Exchange(int index, T item, out T previous)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            return ExchangeInternal(index, item, out previous);
        }

        /// <summary>
        /// Returns an <see cref="System.Collections.Generic.IEnumerator{T}" /> that allows to iterate through the collection.
        /// </summary>
        /// <returns>
        /// An <see cref="System.Collections.Generic.IEnumerator{T}" /> object that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            foreach (var p in EnumeratePrivate())
            {
                yield return p;
            }
        }

        /// <summary>
        /// Inserts the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <returns>
        ///   <c>true</c> if the item was inserted; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity.</exception>
        /// <remarks>
        /// The insertion can fail if the index is already used or is being written by another thread.
        /// If the index is being written it can be understood that the insert operation happened before but the item was overwritten or removed.
        /// </remarks>
        public bool Insert(int index, T item)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity.");
            }
            return InsertInternal(index, item);
        }

        /// <summary>
        /// Inserts the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <param name="previous">The previous item in the specified index.</param>
        /// <returns>
        ///   <c>true</c> if the item was inserted; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        /// <remarks>
        /// The insertion can fail if the index is already used or is being written by another thread.
        /// If the index is being written it can be understood that the insert operation happened before but the item was overwritten or removed.
        /// </remarks>
        public bool Insert(int index, T item, out T previous)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            return InsertInternal(index, item, out previous);
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>
        ///   <c>true</c> if the item was removed; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            object found;
            if (RemoveAtPrivate(index, out found))
            {
                Interlocked.Decrement(ref _count);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="previous">The previous item in the specified index.</param>
        /// <returns>
        ///   <c>true</c> if the item was removed; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public bool RemoveAt(int index, out T previous)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            return RemoveAtInternal(index, out previous);
        }

        /// <summary>
        /// Removes the item at the specified index if it matches the specified value.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value intended to remove.</param>
        /// <returns>
        ///   <c>true</c> if the item was removed; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public bool RemoveValueAt(int index, T value)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            if (RemoveValueAtPrivate(index, value))
            {
                Interlocked.Decrement(ref _count);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <param name="isNew">if set to <c>true</c> the index was not previously used.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public void Set(int index, T item, out bool isNew)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            SetInternal(index, item, out isNew);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Tries to retrieve the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the item was retrieved; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public bool TryGet(int index, out T value)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity");
            }
            return TryGetInternal(index, out value);
        }

        /// <summary>
        /// Replaces the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The new item.</param>
        /// <param name="comparisonItem">The old item.</param>
        /// <param name="previous">The previous item in the specified index.</param>
        /// <param name="isNew">if set to <c>true</c> the index was not previously used.</param>
        /// <returns>
        ///   <c>true</c> if the item was inserted; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity.</exception>
        /// <remarks>
        /// The insertion can fail if the index is already used or is being written by another thread.
        /// If the index is being written it can be understood that the insert operation happened before but the item was overwritten or removed.
        /// </remarks>
        public bool Update(int index, T item, T comparisonItem, out T previous, out bool isNew)
        {
            if (index < 0 || index >= _capacity)
            {
                throw new ArgumentOutOfRangeException("index", "index must be greater or equal to 0 and less than capacity.");
            }
            return UpdateInternal(index, item, comparisonItem, out previous, out isNew);
        }

        internal bool ExchangeInternal(int index, T item, out T previous)
        {
            previous = default(T);
            object found;
            ExchangePrivate(index, item, out found);
            if (found == null)
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            if (!ReferenceEquals(found, BucketHelper.Null))
            {
                previous = (T)found;
            }
            return false;
        }

        internal bool InsertInternal(int index, T item, out T previous)
        {
            object found;
            if (InsertPrivate(index, item, out found))
            {
                previous = default(T);
                Interlocked.Increment(ref _count);
                return true;
            }
            if (ReferenceEquals(found, BucketHelper.Null))
            {
                previous = default(T);
            }
            else
            {
                previous = (T)found;
            }
            return false;
        }

        internal bool InsertInternal(int index, T item)
        {
            object found;
            if (InsertPrivate(index, item, out found))
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            return false;
        }

        internal bool RemoveAtInternal(int index, out T previous)
        {
            object found;
            if (RemoveAtPrivate(index, out found))
            {
                Interlocked.Decrement(ref _count);
                if (ReferenceEquals(found, BucketHelper.Null))
                {
                    previous = default(T);
                }
                else
                {
                    previous = (T)found;
                }
                return true;
            }
            previous = default(T);
            return false;
        }

        internal void SetInternal(int index, T item, out bool isNew)
        {
            SetPrivate(index, item, out isNew);
            if (isNew)
            {
                Interlocked.Increment(ref _count);
            }
        }

        internal bool TryGetInternal(int index, out T value)
        {
            var entry = GetPrivate(index);
            if (entry == null)
            {
                value = default(T);
                return false;
            }
            if (ReferenceEquals(entry, BucketHelper.Null))
            {
                value = default(T);
            }
            else
            {
                value = (T)entry;
            }
            return true;
        }

        internal bool UpdateInternal(int index, T item, T comparisonItem, out T previous, out bool isNew)
        {
            previous = default(T);
            isNew = false;
            object found;
            if (UpdatePrivate(index, item, comparisonItem, out found))
            {
                if (found == null)
                {
                    Interlocked.Increment(ref _count);
                    isNew = true;
                }
                if (!ReferenceEquals(found, BucketHelper.Null))
                {
                    previous = (T) found;
                }
                return true;
            }
            if (found == null)
            {
                isNew = true;
            }
            return false;
        }

        private void CopyToPrivate(T[] array, int arrayIndex)
        {
            foreach (var entry in _entries)
            {
                if (entry != null)
                {
                    if (ReferenceEquals(entry, BucketHelper.Null))
                    {
                        array[arrayIndex] = default(T);
                    }
                    else
                    {
                        array[arrayIndex] = (T)entry;
                    }
                    arrayIndex++;
                }
            }
        }

        private IEnumerable<T> EnumeratePrivate()
        {
            foreach (var entry in _entries)
            {
                if (entry != null)
                {
                    if (ReferenceEquals(entry, BucketHelper.Null))
                    {
                        yield return default(T);
                    }
                    else
                    {
                        yield return (T)entry;
                    }
                }
            }
        }

        private void ExchangePrivate(int index, object item, out object previous)
        {
            previous = Interlocked.Exchange(ref _entries[index], item ?? BucketHelper.Null);
        }

        private object GetPrivate(int index)
        {
            return Interlocked.CompareExchange(ref _entries[index], null, null);
        }

        private int Grow(int capacity)
        {
            var old = _entries;
            _entries = ArrayReservoir<object>.GetArray(capacity);
            if (old != null)
            {
                Array.Copy(old, 0, _entries, 0, _count);
                ArrayReservoir<object>.DonateArray(old);
            }
            return _entries.Length;
        }

        private bool InsertPrivate(int index, object item, out object previous)
        {
            previous = Interlocked.CompareExchange(ref _entries[index], item ?? BucketHelper.Null, null);
            return previous == null;
        }

        private void RecyclePrivate()
        {
            if (!AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
                ArrayReservoir<object>.DonateArray(_entries);
                _entries = null;
            }
        }

        private bool RemoveAtPrivate(int index, out object previous)
        {
            previous = Interlocked.Exchange(ref _entries[index], null);
            return previous != null;
        }

        private bool RemoveValueAtPrivate(int index, object value)
        {
            return Interlocked.CompareExchange(ref _entries[index], null, value) != null;
        }

        private void SetPrivate(int index, object item, out bool isNew)
        {
            isNew = Interlocked.Exchange(ref _entries[index], item ?? BucketHelper.Null) == null;
        }

        [Obsolete("Use only during initialization")]
        private void SetPrivate(int index, object item)
        {
            _entries[index] = item ?? BucketHelper.Null;
        }

        private bool UpdatePrivate(int index, object item, object comparisonItem, out object previous)
        {
            var check = comparisonItem ?? BucketHelper.Null;
            previous = Interlocked.CompareExchange(ref _entries[index], item ?? BucketHelper.Null, check);
            return previous == check;
        }
    }
}