// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Versioning;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    public class NamedPipeTransportBindingElement : ConnectionOrientedTransportBindingElement
    {
        public NamedPipeTransportBindingElement() : base() { }
        protected NamedPipeTransportBindingElement(NamedPipeTransportBindingElement elementToBeCloned) : base(elementToBeCloned) { }

        // AllowedSecurityIdentifiers has moved to NamedPipeListenOptions.AllowedUsers

        public override string Scheme => Uri.UriSchemeNetPipe;
        protected override string WsdlTransportUri => "http://schemas.microsoft.com/soap/named-pipe"; //TransportPolicyConstants.NamedPipeTransportUri
        public override BindingElement Clone() => new NamedPipeTransportBindingElement(this);
        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher) => innerDispatcher;
        protected override bool SupportsUpgrade(StreamUpgradeBindingElement upgradeBindingElement) => upgradeBindingElement is not SslStreamSecurityBindingElement;

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(IBindingDeliveryCapabilities))
            {
                return (T)(object)new BindingDeliveryCapabilitiesHelper();
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        private class BindingDeliveryCapabilitiesHelper : IBindingDeliveryCapabilities
        {
            internal BindingDeliveryCapabilitiesHelper() { }
            bool IBindingDeliveryCapabilities.AssuresOrderedDelivery => true;
            bool IBindingDeliveryCapabilities.QueuedDelivery => false;
        }

    }
}
