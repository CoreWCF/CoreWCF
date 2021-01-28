// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreWCF.Collections.Generic
{
    public class KeyedByTypeCollection<TItem> : KeyedCollection<Type, TItem>
    {
        public KeyedByTypeCollection()
            : base(null, 4)
        {
        }

        public KeyedByTypeCollection(IEnumerable<TItem> items)
            : base(null, 4)
        {
            if (items == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(items));
            }

            foreach (TItem item in items)
            {
                Add(item);
            }
        }

        public T Find<T>()
        {
            return Find<T>(false);
        }

        public T Remove<T>()
        {
            return Find<T>(true);
        }

        private T Find<T>(bool remove)
        {
            for (int i = 0; i < Count; i++)
            {
                TItem settings = this[i];
                if (settings is T)
                {
                    if (remove)
                    {
                        Remove(settings);
                    }
                    return (T)(object)settings;
                }
            }
            return default(T);
        }

        public Collection<T> FindAll<T>()
        {
            return FindAll<T>(false);
        }

        public Collection<T> RemoveAll<T>()
        {
            return FindAll<T>(true);
        }

        private Collection<T> FindAll<T>(bool remove)
        {
            Collection<T> result = new Collection<T>();
            foreach (TItem settings in this)
            {
                if (settings is T)
                {
                    result.Add((T)(object)settings);
                }
            }

            if (remove)
            {
                foreach (T settings in result)
                {
                    Remove((TItem)(object)settings);
                }
            }

            return result;
        }

        protected override Type GetKeyForItem(TItem item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }

            return item.GetType();
        }

        protected override void InsertItem(int index, TItem item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }

            if (Contains(item.GetType()))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(item), SR.Format(SR.DuplicateBehavior1, item.GetType().FullName));
            }

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, TItem item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }

            base.SetItem(index, item);
        }
    }
}