// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Security;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public class UnixPosixIdentityBindingElement : StreamUpgradeBindingElement
    {
        private ProtectionLevel _protectionLevel;

        public UnixPosixIdentityBindingElement()
            : base()
        {
            _protectionLevel = ConnectionOrientedTransportDefaults.ProtectionLevel;
        }

        protected UnixPosixIdentityBindingElement(UnixPosixIdentityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _protectionLevel = elementToBeCloned._protectionLevel;
        }

        public override BindingElement Clone()
        {
            return new UnixPosixIdentityBindingElement(this);
        }

        public override StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context)
        {
            if(string.Compare(context.Binding.Scheme, "net.uds", true) != 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(context.Binding.Scheme), SR.UDSUriSchemeWrong);
            }
            return new UnixPosixIdentitySecurityUpgradeProvider(this, context);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return (T)(object)new SecurityCapabilities(true, true, true, _protectionLevel, _protectionLevel);
            }
            else if (typeof(T) == typeof(IdentityVerifier))
            {
                return (T)(object)IdentityVerifier.CreateDefault();
            }
            else
            {
                return context.GetInnerProperty<T>();
            }
        }
    }
}

