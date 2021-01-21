// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Collections.Generic;

namespace CoreWCF
{
    public sealed class ExtensionCollection<T> : SynchronizedCollection<IExtension<T>>, IExtensionCollection<T>
        where T : IExtensibleObject<T>
    {
        private readonly T _owner;

        public ExtensionCollection(T owner)
        {
            if (owner == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(owner));

            _owner = owner;
        }

        public ExtensionCollection(T owner, object syncRoot)
            : base(syncRoot)
        {
            if (owner == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(owner));

            _owner = owner;
        }

        bool ICollection<IExtension<T>>.IsReadOnly
        {
            get { return false; }
        }

        protected override void ClearItems()
        {
            IExtension<T>[] array;

            lock (SyncRoot)
            {
                array = new IExtension<T>[Count];
                CopyTo(array, 0);
                base.ClearItems();

                foreach (IExtension<T> extension in array)
                {
                    extension.Detach(_owner);
                }
            }
        }

        public TE Find<TE>()
        {
            List<IExtension<T>> items = Items;

            lock (SyncRoot)
            {
                for (int i = Count - 1; i >= 0; i--)
                {
                    IExtension<T> item = items[i];
                    if (item is TE)
                        return (TE)item;
                }
            }

            return default(TE);
        }

        public Collection<TE> FindAll<TE>()
        {
            Collection<TE> result = new Collection<TE>();
            List<IExtension<T>> items = Items;

            lock (SyncRoot)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    IExtension<T> item = items[i];
                    if (item is TE)
                        result.Add((TE)item);
                }
            }

            return result;
        }

        protected override void InsertItem(int index, IExtension<T> item)
        {
            if (item == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));

            lock (SyncRoot)
            {
                item.Attach(_owner);
                base.InsertItem(index, item);
            }
        }

        protected override void RemoveItem(int index)
        {
            lock (SyncRoot)
            {
                Items[index].Detach(_owner);
                base.RemoveItem(index);
            }
        }

        protected override void SetItem(int index, IExtension<T> item)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCannotSetExtensionsByIndex));
        }
    }

}