// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Collections.Generic;

namespace CoreWCF
{
    public class UriSchemeKeyedCollection : SynchronizedKeyedCollection<string, Uri>
    {
        internal UriSchemeKeyedCollection(object syncRoot)
            : base(syncRoot)
        {
        }

        public UriSchemeKeyedCollection(params Uri[] addresses)
        {
            if (addresses == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addresses));
            }

            for (int i = 0; i < addresses.Length; i++)
            {
                Add(addresses[i]);
            }
        }

        protected override string GetKeyForItem(Uri item)
        {
            return item.Scheme + ":" + item.Port;
        }

        protected override void InsertItem(int index, Uri item)
        {
            ValidateBaseAddress(item, "item");
            if (Contains(item.Scheme))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(item), SR.Format(SR.BaseAddressDuplicateScheme, item.Scheme));
            }

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, Uri item)
        {
            ValidateBaseAddress(item, "item");
            if (this[index].Scheme != item.Scheme)
            {
                if (Contains(item.Scheme))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(item), SR.Format(SR.BaseAddressDuplicateScheme, item.Scheme));
                }
            }
            base.SetItem(index, item);
        }

        internal static void ValidateBaseAddress(Uri uri, string argumentName)
        {
            if (uri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(argumentName);
            }

            if (!uri.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressMustBeAbsolute);
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressCannotHaveUserInfo);
            }

            if (!string.IsNullOrEmpty(uri.Query))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressCannotHaveQuery);
            }

            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressCannotHaveFragment);
            }
        }
    }
}
