// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public class WindowsStreamSecurityBindingElement : StreamUpgradeBindingElement
    {
        private ProtectionLevel _protectionLevel;

        public WindowsStreamSecurityBindingElement()
            : base()
        {
            _protectionLevel = ConnectionOrientedTransportDefaults.ProtectionLevel;
        }

        protected WindowsStreamSecurityBindingElement(WindowsStreamSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _protectionLevel = elementToBeCloned._protectionLevel;
        }

        public ProtectionLevel ProtectionLevel
        {
            get
            {
                return _protectionLevel;
            }
            set
            {
                ProtectionLevelHelper.Validate(value);
                _protectionLevel = value;
            }
        }

        public override BindingElement Clone()
        {
            return new WindowsStreamSecurityBindingElement(this);
        }

        public override StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context)
        {
            return new WindowsStreamSecurityUpgradeProvider(this, context, false);
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
