// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;

namespace Helpers
{
    internal class ListenAddressVerifyingBindingElement : StreamUpgradeBindingElement
    {
        public Uri ListenUriBaseAddress { get; private set; }

        public override StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context)
        {
            ListenUriBaseAddress = context.ListenUriBaseAddress;
            return null;
        }

        public override BindingElement Clone() => this;
        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.GetInnerProperty<T>();
        }
    }
}
