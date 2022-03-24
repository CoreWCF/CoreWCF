// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public abstract class BindingElementExtensionElement : ServiceModelExtensionElement
    {
        protected internal abstract BindingElement CreateBindingElement();
        public abstract Type BindingElementType { get; }

        protected internal virtual void InitializeFrom(BindingElement bindingElement)
        {

        }

        public virtual void ApplyConfiguration(BindingElement bindingElement)
        {

        }

    }
}
