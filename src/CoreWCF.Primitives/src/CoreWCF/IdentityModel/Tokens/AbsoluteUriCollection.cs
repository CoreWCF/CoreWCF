// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// A collection of absolute URIs.
    /// </summary>
    internal class AbsoluteUriCollection : Collection<Uri>
    {
        public AbsoluteUriCollection()
        {
        }

        protected override void InsertItem(int index, Uri item)
        {
            if (null == item || !item.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(item), SR.Format(SR.ID0013));
            }
            
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, Uri item)
        {
            if (null == item || !item.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(item), SR.Format(SR.ID0013));
            }

            base.SetItem(index, item);
        }
    }
}
