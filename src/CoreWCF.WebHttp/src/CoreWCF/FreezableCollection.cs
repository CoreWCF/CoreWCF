// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreWCF
{
    internal class FreezableCollection<T> : Collection<T>, ICollection<T>
    {
        private bool _frozen;

        public FreezableCollection()
            : base()
        {
        }

        public FreezableCollection(IList<T> list)
            : base(list)
        {
        }

        public bool IsFrozen
        {
            get
            {
                return _frozen;
            }
        }

        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return _frozen;
            }
        }

        public void Freeze()
        {
            _frozen = true;
        }

        protected override void ClearItems()
        {
            ThrowIfFrozen();
            base.ClearItems();
        }

        protected override void InsertItem(int index, T item)
        {
            ThrowIfFrozen();
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ThrowIfFrozen();
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, T item)
        {
            ThrowIfFrozen();
            base.SetItem(index, item);
        }

        private void ThrowIfFrozen()
        {
            if (_frozen)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
