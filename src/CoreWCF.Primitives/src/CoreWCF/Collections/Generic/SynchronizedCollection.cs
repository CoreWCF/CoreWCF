// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CoreWCF.Collections.Generic
{
    public class SynchronizedCollection<T> : IList<T>, IList
    {
        private readonly List<T> _items;
        private readonly object _sync;

        public SynchronizedCollection()
        {
            _items = new List<T>();
            _sync = new object();
        }

        public SynchronizedCollection(object syncRoot)
        {
            if (syncRoot == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));

            _items = new List<T>();
            _sync = syncRoot;
        }

        public SynchronizedCollection(object syncRoot, IEnumerable<T> list)
        {
            if (syncRoot == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));
            if (list == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(list));

            _items = new List<T>(list);
            _sync = syncRoot;
        }

        public SynchronizedCollection(object syncRoot, params T[] list)
        {
            if (syncRoot == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));
            if (list == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(list));

            _items = new List<T>(list.Length);
            foreach (T t in list)
                _items.Add(t);

            _sync = syncRoot;
        }

        public int Count
        {
            get { lock (_sync) { return _items.Count; } }
        }

        protected List<T> Items
        {
            get { return _items; }
        }

        public object SyncRoot
        {
            get { return _sync; }
        }

        public T this[int index]
        {
            get
            {
                lock (_sync)
                {
                    return _items[index];
                }
            }
            set
            {
                lock (_sync)
                {
                    if (index < 0 || index >= _items.Count)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                    SR.Format(SR.ValueMustBeInRange, 0, _items.Count - 1)));

                    SetItem(index, value);
                }
            }
        }

        public void Add(T item)
        {
            lock (_sync)
            {
                int index = _items.Count;
                InsertItem(index, item);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                ClearItems();
            }
        }

        public void CopyTo(T[] array, int index)
        {
            lock (_sync)
            {
                _items.CopyTo(array, index);
            }
        }

        public bool Contains(T item)
        {
            lock (_sync)
            {
                return _items.Contains(item);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (_sync)
            {
                return _items.GetEnumerator();
            }
        }

        public int IndexOf(T item)
        {
            lock (_sync)
            {
                return InternalIndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (_sync)
            {
                if (index < 0 || index > _items.Count)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                SR.Format(SR.ValueMustBeInRange, 0, _items.Count - 1)));

                InsertItem(index, item);
            }
        }

        private int InternalIndexOf(T item)
        {
            int count = _items.Count;

            for (int i = 0; i < count; i++)
            {
                if (Equals(_items[i], item))
                {
                    return i;
                }
            }
            return -1;
        }

        public bool Remove(T item)
        {
            lock (_sync)
            {
                int index = InternalIndexOf(item);
                if (index < 0)
                    return false;

                RemoveItem(index);
                return true;
            }
        }

        public void RemoveAt(int index)
        {
            lock (_sync)
            {
                if (index < 0 || index >= _items.Count)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                SR.Format(SR.ValueMustBeInRange, 0, _items.Count - 1)));

                RemoveItem(index);
            }
        }

        protected virtual void ClearItems()
        {
            _items.Clear();
        }

        protected virtual void InsertItem(int index, T item)
        {
            _items.Insert(index, item);
        }

        protected virtual void RemoveItem(int index)
        {
            _items.RemoveAt(index);
        }

        protected virtual void SetItem(int index, T item)
        {
            _items[index] = item;
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return false; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList)_items).GetEnumerator();
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot
        {
            get { return _sync; }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            lock (_sync)
            {
                ((IList)_items).CopyTo(array, index);
            }
        }

        object IList.this[int index]
        {
            get
            {
                return this[index];
            }
            set
            {
                VerifyValueType(value);
                this[index] = (T)value;
            }
        }

        bool IList.IsReadOnly
        {
            get { return false; }
        }

        bool IList.IsFixedSize
        {
            get { return false; }
        }

        int IList.Add(object value)
        {
            VerifyValueType(value);

            lock (_sync)
            {
                Add((T)value);
                return Count - 1;
            }
        }

        bool IList.Contains(object value)
        {
            VerifyValueType(value);
            return Contains((T)value);
        }

        int IList.IndexOf(object value)
        {
            VerifyValueType(value);
            return IndexOf((T)value);
        }

        void IList.Insert(int index, object value)
        {
            VerifyValueType(value);
            Insert(index, (T)value);
        }

        void IList.Remove(object value)
        {
            VerifyValueType(value);
            Remove((T)value);
        }

        private static void VerifyValueType(object value)
        {
            if (value == null)
            {
                if (typeof(T).GetTypeInfo().IsValueType)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SynchronizedCollectionWrongTypeNull);
                }
            }
            else if (!(value is T))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SynchronizedCollectionWrongType1, value.GetType().FullName));
            }
        }
    }

}