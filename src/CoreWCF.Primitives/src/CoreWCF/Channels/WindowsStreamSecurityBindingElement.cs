using CoreWCF.Security;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;

namespace CoreWCF.Channels
{
    public class WindowsStreamSecurityBindingElement : StreamUpgradeBindingElement
    {
        ProtectionLevel _protectionLevel;

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
