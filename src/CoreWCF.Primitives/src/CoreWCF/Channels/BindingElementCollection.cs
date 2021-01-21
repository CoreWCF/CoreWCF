// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreWCF.Channels
{
    public class BindingElementCollection : Collection<BindingElement>
    {
        public BindingElementCollection()
        {
        }

        public BindingElementCollection(IEnumerable<BindingElement> elements)
        {
            if (elements == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elements));

            foreach (BindingElement element in elements)
            {
                Add(element);
            }
        }

        public BindingElementCollection(BindingElement[] elements)
        {
            if (elements == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elements));

            for (int i = 0; i < elements.Length; i++)
            {
                Add(elements[i]);
            }
        }

        // returns a new collection with clones of all the elements
        public BindingElementCollection Clone()
        {
            BindingElementCollection result = new BindingElementCollection();
            for (int i = 0; i < Count; i++)
            {
                result.Add(this[i].Clone());
            }
            return result;
        }

        public void AddRange(params BindingElement[] elements)
        {
            if (elements == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elements));

            for (int i = 0; i < elements.Length; i++)
            {
                Add(elements[i]);
            }
        }

        public bool Contains(Type bindingElementType)
        {
            if (bindingElementType == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingElementType));

            for (int i = 0; i < Count; i++)
            {
                if (bindingElementType.IsInstanceOfType(this[i]))
                    return true;
            }
            return false;
        }

        public T Find<T>()
        {
            return Find<T>(false);
        }

        public T Remove<T>()
        {
            return Find<T>(true);
        }

        T Find<T>(bool remove)
        {
            for (int index = 0; index < Count; index++)
            {
                if (this[index] is T)
                {
                    T item = (T)(object)this[index];
                    if (remove)
                    {
                        RemoveAt(index);
                    }
                    return item;
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

        Collection<T> FindAll<T>(bool remove)
        {
            Collection<T> collection = new Collection<T>();

            for (int index = 0; index < Count; index++)
            {
                if (this[index] is T)
                {
                    T item = (T)(object)this[index];
                    if (remove)
                    {
                        RemoveAt(index);
                        // back up the index so we inspect the new item at this location
                        index--;
                    }
                    collection.Add(item);
                }
            }

            return collection;
        }
        protected override void InsertItem(int index, BindingElement item)
        {
            if (item == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, BindingElement item)
        {
            if (item == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));

            base.SetItem(index, item);
        }

        internal BindingElementCollection Reverse()
        {
            var bec = new BindingElementCollection();
            for (int i = Count - 1; i >= 0; i--)
            {
                bec.Add(this[i]);
            }

            return bec;
        }
    }
}