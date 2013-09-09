﻿#if FAT

using System;
using System.Collections.Generic;
using Theraot.Collections;
using Theraot.Collections.Specialized;
using Theraot.Collections.ThreadSafe;
using Theraot.Core;
using Theraot.Threading;
using Theraot.Threading.Needles;

namespace Theraot.Collections.ThreadSafe
{
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Diagnostics.DebuggerDisplay("Count={Count}")]
    public class WeakSetBucket<T, TNeedle> : ICollection<T>, IEnumerable<T>, ISet<T>, IEqualityComparer<T>
        where T : class
        where TNeedle : WeakNeedle<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private readonly SetBucket<TNeedle> _wrapped;
        private EventHandler _eventHandler;

        public WeakSetBucket()
        {
            RegisterForAutoRemoveDeadItems();
        }

        public WeakSetBucket(IEnumerable<T> prototype)
            : this()
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(T[] prototype)
            : this()
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(IEnumerable<T> prototype, IEqualityComparer<T> comparer)
            : this(comparer)
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(T[] prototype, IEqualityComparer<T> comparer)
            : this(comparer)
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(IEqualityComparer<T> comparer)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _wrapped = new SetBucket<TNeedle>
            (
                new ConversionEqualityComparer<TNeedle, T>(_comparer, input => input.Value)
            );
            RegisterForAutoRemoveDeadItems();
        }

        public WeakSetBucket(bool autoRemoveDeadItems)
        {
            if (autoRemoveDeadItems)
            {
                RegisterForAutoRemoveDeadItems();
            }
        }

        public WeakSetBucket(IEnumerable<T> prototype, bool autoRemoveDeadItems)
            : this(autoRemoveDeadItems)
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(T[] prototype, bool autoRemoveDeadItems)
            : this(autoRemoveDeadItems)
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(IEnumerable<T> prototype, IEqualityComparer<T> comparer, bool autoRemoveDeadItems)
            : this(comparer, autoRemoveDeadItems)
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
            RegisterForAutoRemoveDeadItems();
        }

        public WeakSetBucket(T[] prototype, IEqualityComparer<T> comparer, bool autoRemoveDeadItems)
            : this(comparer, autoRemoveDeadItems)
        {
            Check.NotNullArgument(prototype, "prototype");
            this.AddRange(prototype);
        }

        public WeakSetBucket(IEqualityComparer<T> comparer, bool autoRemoveDeadItems)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _wrapped = new SetBucket<TNeedle>
            (
                new ConversionEqualityComparer<TNeedle, T>(_comparer, input => input.Value)
            );
            if (autoRemoveDeadItems)
            {
                RegisterForAutoRemoveDeadItems();
            }
        }

        public bool AutoRemoveDeadItems
        {
            get
            {
                return !object.ReferenceEquals(_eventHandler, null);
            }
            set
            {
                if (value)
                {
                    RegisterForAutoRemoveDeadItems();
                }
                else
                {
                    UnRegisterForAutoRemoveDeadItems();
                }
            }
        }

        public int Count
        {
            get
            {
                return _wrapped.Count;
            }
        }

        [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "Returns false")]
        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return false;
            }
        }

        protected SetBucket<TNeedle> Wrapped
        {
            get
            {
                return _wrapped;
            }
        }

        public bool Add(T item)
        {
            return _wrapped.Add(NeedleHelper.CreateNeedle<T, TNeedle>(item));
        }

        public void Clear()
        {
            _wrapped.Clear();
        }

        public WeakSetBucket<T, TNeedle> Clone()
        {
            return OnClone();
        }

        public bool Contains(T item)
        {
            foreach (var _item in this)
            {
                if (_comparer.Equals(_item, item))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Extensions.CopyTo(this, array, arrayIndex);
        }

        public bool Equals(T x, T y)
        {
            return _comparer.Equals(x, y);
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            Extensions.ExceptWith(this, other);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _wrapped.ConvertProgressiveFiltered(input => input.Value, input => input.IsAlive).GetEnumerator();
        }

        public int GetHashCode(T obj)
        {
            return _comparer.GetHashCode(obj);
        }

        void ICollection<T>.Add(T item)
        {
            _wrapped.Add(NeedleHelper.CreateNeedle<T, TNeedle>(item));
        }
        public void IntersectWith(IEnumerable<T> other)
        {
            Extensions.IntersectWith(this, other);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return Extensions.IsProperSubsetOf(this, other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return Extensions.IsProperSupersetOf(this, other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return Extensions.IsSubsetOf(this, other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return Extensions.IsSupersetOf(this, other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return Extensions.Overlaps(this, other);
        }

        public bool Remove(T item)
        {
            return _wrapped.Remove(NeedleHelper.CreateNeedle<T, TNeedle>(item));
        }

        public int RemoveDeadItems()
        {
            return Extensions.RemoveWhere<TNeedle>
                (
                    _wrapped,
                    items =>
                    {
                        return items.WhereMatch(input => !input.IsAlive);
                    }
                );
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return Extensions.SetEquals(this, other);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            Extensions.SymmetricExceptWith(this, other);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void UnionWith(IEnumerable<T> other)
        {
            Extensions.UnionWith(this, other);
        }

        protected virtual WeakSetBucket<T, TNeedle> OnClone()
        {
            return new WeakSetBucket<T, TNeedle>(this as IEnumerable<T>, _comparer);
        }
        private void GarbageCollected(object sender, EventArgs e)
        {
            RemoveDeadItems();
        }

        private void RegisterForAutoRemoveDeadItems()
        {
            _eventHandler = new EventHandler(GarbageCollected);
            GCMonitor.Collected += _eventHandler;
        }

        private void UnRegisterForAutoRemoveDeadItems()
        {
            if (!object.ReferenceEquals(_eventHandler, null))
            {
                GCMonitor.Collected -= _eventHandler;
                _eventHandler = null;
            }
        }
    }
}

#endif