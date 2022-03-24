// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class TcpTransportElement : ConnectionOrientedTransportElement
    {
        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);
            TcpTransportBindingElement binding = (TcpTransportBindingElement)bindingElement;
            PropertyInformationCollection propertyInfo = this.ElementInformation.Properties;
            if (this.ListenBacklog != TcpTransportDefaults.ListenBacklogConst)
            {
                binding.ListenBacklog = this.ListenBacklog;
            }
            //binding.PortSharingEnabled = this.PortSharingEnabled;
            //binding.TeredoEnabled = this.TeredoEnabled;
            this.ConnectionPoolSettings.ApplyConfiguration(binding.ConnectionPoolSettings);
            binding.ExtendedProtectionPolicy = ConfigurationChannelBindingUtility.BuildPolicy(this.ExtendedProtectionPolicy);
        }

        public override Type BindingElementType
        {
            get { return typeof(TcpTransportBindingElement); }
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            TcpTransportElement source = (TcpTransportElement)from;
            this.ListenBacklog = source.ListenBacklog;
            //this.PortSharingEnabled = source.PortSharingEnabled;
            //this.TeredoEnabled = source.TeredoEnabled;
            this.ConnectionPoolSettings.CopyFrom(source.ConnectionPoolSettings);
            ConfigurationChannelBindingUtility.CopyFrom(source.ExtendedProtectionPolicy, this.ExtendedProtectionPolicy);
        }

        protected override TransportBindingElement CreateDefaultBindingElement()
        {
            return new TcpTransportBindingElement();
        }

        protected internal override void InitializeFrom(BindingElement bindingElement)
        {
            base.InitializeFrom(bindingElement);
            TcpTransportBindingElement binding = (TcpTransportBindingElement)bindingElement;

            ConfigurationProperty listenBacklogProperty = this.Properties[ConfigurationStrings.ListenBacklog];
            SetPropertyValue(listenBacklogProperty, binding.ListenBacklog, false /*ignore locks*/);
            
            //SetPropertyValueIfNotDefaultValue(ConfigurationStrings.PortSharingEnabled, binding.PortSharingEnabled);
            //SetPropertyValueIfNotDefaultValue(ConfigurationStrings.TeredoEnabled, binding.TeredoEnabled);
            this.ConnectionPoolSettings.InitializeFrom(binding.ConnectionPoolSettings);
            ConfigurationChannelBindingUtility.InitializeFrom(binding.ExtendedProtectionPolicy, this.ExtendedProtectionPolicy);
        }

        [ConfigurationProperty(ConfigurationStrings.ListenBacklog, DefaultValue = TcpTransportDefaults.ListenBacklogConst)]
        [IntegerValidator(MinValue = 0)]
        public int ListenBacklog
        {
            get { return (int)base[ConfigurationStrings.ListenBacklog]; }
            set { base[ConfigurationStrings.ListenBacklog] = value; }
        }
        
        //[ConfigurationProperty(ConfigurationStrings.PortSharingEnabled, DefaultValue = TcpTransportDefaults.PortSharingEnabled)]
        //public bool PortSharingEnabled
        //{
        //    get { return (bool)base[ConfigurationStrings.PortSharingEnabled]; }
        //    set { base[ConfigurationStrings.PortSharingEnabled] = value; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.TeredoEnabled, DefaultValue = TcpTransportDefaults.TeredoEnabled)]
        //public bool TeredoEnabled
        //{
        //    get { return (bool)base[ConfigurationStrings.TeredoEnabled]; }
        //    set { base[ConfigurationStrings.TeredoEnabled] = value; }
        //}

        [ConfigurationProperty(ConfigurationStrings.ConnectionPoolSettings)]
        public TcpConnectionPoolSettingsElement ConnectionPoolSettings
        {
            get { return (TcpConnectionPoolSettingsElement)base[ConfigurationStrings.ConnectionPoolSettings]; }
            set { base[ConfigurationStrings.ConnectionPoolSettings] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ExtendedProtectionPolicy)]
        public ExtendedProtectionPolicyElement ExtendedProtectionPolicy
        {
            get { return (ExtendedProtectionPolicyElement)base[ConfigurationStrings.ExtendedProtectionPolicy]; }
            private set { base[ConfigurationStrings.ExtendedProtectionPolicy] = value; }
        }
    }
}
