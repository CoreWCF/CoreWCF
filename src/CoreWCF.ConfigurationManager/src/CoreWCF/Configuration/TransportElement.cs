// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public abstract class TransportElement : BindingElementExtensionElement
    {
        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);
            TransportBindingElement binding = (TransportBindingElement)bindingElement;
            binding.ManualAddressing = this.ManualAddressing;
            binding.MaxBufferPoolSize = this.MaxBufferPoolSize;
            binding.MaxReceivedMessageSize = this.MaxReceivedMessageSize;
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            TransportElement source = (TransportElement)from;
            this.ManualAddressing = source.ManualAddressing;
            this.MaxBufferPoolSize = source.MaxBufferPoolSize;
            this.MaxReceivedMessageSize = source.MaxReceivedMessageSize;
        }

        protected internal override BindingElement CreateBindingElement()
        {
            TransportBindingElement binding = this.CreateDefaultBindingElement();
            this.ApplyConfiguration(binding);
            return binding;
        }

        protected abstract TransportBindingElement CreateDefaultBindingElement();

        protected internal override void InitializeFrom(BindingElement bindingElement)
        {
            base.InitializeFrom(bindingElement);
            TransportBindingElement binding = (TransportBindingElement)bindingElement;
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ManualAddressing, binding.ManualAddressing);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxBufferPoolSize, binding.MaxBufferPoolSize);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxReceivedMessageSize, binding.MaxReceivedMessageSize);
        }

        [ConfigurationProperty(ConfigurationStrings.ManualAddressing, DefaultValue = false)]
        public bool ManualAddressing
        {
            get { return (bool)base[ConfigurationStrings.ManualAddressing]; }
            set { base[ConfigurationStrings.ManualAddressing] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBufferPoolSize, DefaultValue = TransportDefaults.MaxBufferPoolSize)]
        [LongValidator(MinValue = 1)]
        public long MaxBufferPoolSize
        {
            get { return (long)base[ConfigurationStrings.MaxBufferPoolSize]; }
            set { base[ConfigurationStrings.MaxBufferPoolSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxReceivedMessageSize, DefaultValue = TransportDefaults.MaxReceivedMessageSize)]
        [LongValidator(MinValue = 1)]
        public long MaxReceivedMessageSize
        {
            get { return (long)base[ConfigurationStrings.MaxReceivedMessageSize]; }
            set { base[ConfigurationStrings.MaxReceivedMessageSize] = value; }
        }
    }
}
