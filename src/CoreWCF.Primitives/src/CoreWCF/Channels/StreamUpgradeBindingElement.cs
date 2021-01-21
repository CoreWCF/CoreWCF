// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public abstract class StreamUpgradeBindingElement : BindingElement
    {
        protected StreamUpgradeBindingElement()
        {
        }

        protected StreamUpgradeBindingElement(StreamUpgradeBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
        }

        public abstract StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context);
    }
}
