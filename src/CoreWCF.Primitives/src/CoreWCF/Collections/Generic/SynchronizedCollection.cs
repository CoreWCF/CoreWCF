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
        public SynchronizedCollection()
        {
            Items = new List<T>();
            SyncRoot = new object();
        }

        public SynchronizedCollection(object syncRoot)
        {
            Items = new List<T>();
            SyncRoot = syncRoot ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));
        }

        public SynchronizedCollection(object syncRoot, IEnumerable<T> list)
        {
            if (list == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(list));
            }

            Items = new List<T>(list);
            SyncRoot = syncRoot ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));
        }

        public SynchronizedCollection(object syncRoot, params T[] list)
        {
            if (list == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(list));
            }

            Items = new List<T>(list.Length);
            foreach (T t in list)
            {
                Items.Add(t);
            }

            SyncRoot = syncRoot ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));
        }

        public int Count
        {
            get { lock (SyncRoot) { return Items.Count; } }
        }

        protected List<T> Items { get; }

        public object SyncRoot { get; }

        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return Items[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    if (index < 0 || index >= Items.Count)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                    SR.Format(SR.ValueMustBeInRange, 0, Items.Count - 1)));
                    }

                    SetItem(index, value);
                }
            }
        }

        public void Add(T item)
        {
            lock (SyncRoot)
            {
                int index = Items.Count;
                InsertItem(index, item);
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                ClearItems();
            }
        }

        public void CopyTo(T[] array, int index)
        {
            lock (SyncRoot)
            {
                Items.CopyTo(array, index);
            }
        }

        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                return Items.Contains(item);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (SyncRoot)
            {
                return Items.GetEnumerator();
            }
        }

        public int IndexOf(T item)
        {
            lock (SyncRoot)
            {
                return InternalIndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (SyncRoot)
            {
                if (index < 0 || index > Items.Count)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                SR.Format(SR.ValueMustBeInRange, 0, Items.Count - 1)));
                }

                InsertItem(index, item);
            }
        }

        private int InternalIndexOf(T item)
        {
            int count = Items.Count;

            for (int i = 0; i < count; i++)
            {
                if (Equals(Items[i], item))
                {
                    return i;
                }
            }
            return -1;
        }

        public bool Remove(T item)
        {
            lock (SyncRoot)
            {
                int index = InternalIndexOf(item);
                if (index < 0)
                {
                    return false;
                }

                RemoveItem(index);
                return true;
            }
        }

        public void RemoveAt(int index)
        {
            lock (SyncRoot)
            {
                if (index < 0 || index >= Items.Count)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                SR.Format(SR.ValueMustBeInRange, 0, Items.Count - 1)));
                }

                RemoveItem(index);
            }
        }

        protected virtual void ClearItems()
        {
            Items.Clear();
        }

        protected virtual void InsertItem(int index, T item)
        {
            Items.Insert(index, item);
        }

        protected virtual void RemoveItem(int index)
        {
            Items.RemoveAt(index);
        }

        protected virtual void SetItem(int index, T item)
        {
            Items[index] = item;
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return false; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList)Items).GetEnumerator();
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot
        {
            get { return SyncRoot; }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            lock (SyncRoot)
            {
                ((IList)Items).CopyTo(array, index);
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

            lock (SyncRoot)
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