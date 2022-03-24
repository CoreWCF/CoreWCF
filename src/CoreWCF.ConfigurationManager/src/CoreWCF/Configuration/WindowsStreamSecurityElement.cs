// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using System.Net.Security;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class WindowsStreamSecurityElement : BindingElementExtensionElement
    {
        public WindowsStreamSecurityElement()
        {
        }

        [ConfigurationProperty(ConfigurationStrings.ProtectionLevel, DefaultValue = ConnectionOrientedTransportDefaults.ProtectionLevel)]
        public ProtectionLevel ProtectionLevel
        {
            get { return (ProtectionLevel)base[ConfigurationStrings.ProtectionLevel]; }
            set { base[ConfigurationStrings.ProtectionLevel] = value; }
        }

        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);
            WindowsStreamSecurityBindingElement windowsBindingElement =
                (WindowsStreamSecurityBindingElement)bindingElement;
            windowsBindingElement.ProtectionLevel = this.ProtectionLevel;
        }

        protected internal override BindingElement CreateBindingElement()
        {
            WindowsStreamSecurityBindingElement windowsBindingElement
                = new WindowsStreamSecurityBindingElement();

            this.ApplyConfiguration(windowsBindingElement);
            return windowsBindingElement;
        }

        public override Type BindingElementType
        {
            get { return typeof(WindowsStreamSecurityBindingElement); }
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            WindowsStreamSecurityElement source = (WindowsStreamSecurityElement)from;
            this.ProtectionLevel = source.ProtectionLevel;
        }

        protected internal override void InitializeFrom(BindingElement bindingElement)
        {
            base.InitializeFrom(bindingElement);
            WindowsStreamSecurityBindingElement windowsBindingElement
                = (WindowsStreamSecurityBindingElement)bindingElement;
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ProtectionLevel, windowsBindingElement.ProtectionLevel);
        }
    }
}
