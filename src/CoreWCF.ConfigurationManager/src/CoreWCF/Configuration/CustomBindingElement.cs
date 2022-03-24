// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class CustomBindingElement : NamedServiceModelExtensionCollectionElement<BindingElementExtensionElement>, IDefaultCommunicationTimeouts, IStandardBindingElement
    {
        public CustomBindingElement()
            : this(null) { }

        public CustomBindingElement(string name) :
            base(ConfigurationStrings.BindingElementExtensions, name) { }

        [ConfigurationProperty(ConfigurationStrings.CloseTimeout, DefaultValue = ServiceDefaults.CloseTimeoutString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan CloseTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.CloseTimeout]; }
            set
            {
                base[ConfigurationStrings.CloseTimeout] = value;
                this.SetIsModified();
            }
        }

        [ConfigurationProperty(ConfigurationStrings.OpenTimeout, DefaultValue = ServiceDefaults.OpenTimeoutString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan OpenTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.OpenTimeout]; }
            set
            {
                base[ConfigurationStrings.OpenTimeout] = value;
                this.SetIsModified();
            }
        }

        [ConfigurationProperty(ConfigurationStrings.ReceiveTimeout, DefaultValue = ServiceDefaults.ReceiveTimeoutString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan ReceiveTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.ReceiveTimeout]; }
            set
            {
                base[ConfigurationStrings.ReceiveTimeout] = value;
                this.SetIsModified();
            }
        }

        [ConfigurationProperty(ConfigurationStrings.SendTimeout, DefaultValue = ServiceDefaults.SendTimeoutString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan SendTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.SendTimeout]; }
            set
            {
                base[ConfigurationStrings.SendTimeout] = value;
                this.SetIsModified();
            }
        }

        public override void Add(BindingElementExtensionElement element)
        {
            if (null == element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("element");
            }

            BindingElementExtensionElement existingElement = null;
            if (!this.CanAddEncodingElement(element, ref existingElement))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigMessageEncodingAlreadyInBinding,
                    existingElement.ConfigurationElementName,
                    existingElement.GetType().AssemblyQualifiedName)));
            }
            else if (!this.CanAddStreamUpgradeElement(element, ref existingElement))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigStreamUpgradeElementAlreadyInBinding,
                    existingElement.ConfigurationElementName,
                    existingElement.GetType().AssemblyQualifiedName)));
            }
            else if (!this.CanAddTransportElement(element, ref existingElement))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigTransportAlreadyInBinding,
                    existingElement.ConfigurationElementName,
                    existingElement.GetType().AssemblyQualifiedName)));
            }
            else
            {
                base.Add(element);
            }
        }

        public void ApplyConfiguration(Binding binding)
        {
            if (null == binding)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("binding");
            }
            if (binding.GetType() != typeof(CustomBinding))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.ConfigInvalidTypeForBinding,
                    typeof(CustomBinding).AssemblyQualifiedName,
                    binding.GetType().AssemblyQualifiedName));
            }

            binding.Name = this.Name;
            binding.CloseTimeout = this.CloseTimeout;
            binding.OpenTimeout = this.OpenTimeout;
            binding.ReceiveTimeout = this.ReceiveTimeout;
            binding.SendTimeout = this.SendTimeout;

            this.OnApplyConfiguration(binding);
        }

        public override bool CanAdd(BindingElementExtensionElement element)
        {
            if (null == element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("element");
            }

            BindingElementExtensionElement existingElement = null;
            return !this.ContainsKey(element.GetType()) && this.CanAddEncodingElement(element, ref existingElement) &&
                this.CanAddStreamUpgradeElement(element, ref existingElement) && this.CanAddTransportElement(element, ref existingElement);
        }

        private bool CanAddEncodingElement(BindingElementExtensionElement element, ref BindingElementExtensionElement existingElement)
        {
            return this.CanAddExclusiveElement(typeof(MessageEncodingBindingElement), element.BindingElementType, ref existingElement);
        }

        private bool CanAddExclusiveElement(Type exclusiveType, Type bindingElementType, ref BindingElementExtensionElement existingElement)
        {
            bool retval = true;
            if (exclusiveType.IsAssignableFrom(bindingElementType))
            {
                foreach (BindingElementExtensionElement existing in this)
                {
                    if (exclusiveType.IsAssignableFrom(existing.BindingElementType))
                    {
                        retval = false;
                        existingElement = existing;
                        break;
                    }
                }
            }
            return retval;
        }

        private bool CanAddStreamUpgradeElement(BindingElementExtensionElement element, ref BindingElementExtensionElement existingElement)
        {
            return this.CanAddExclusiveElement(typeof(StreamUpgradeBindingElement), element.BindingElementType, ref existingElement);
        }

        private bool CanAddTransportElement(BindingElementExtensionElement element, ref BindingElementExtensionElement existingElement)
        {
            return this.CanAddExclusiveElement(typeof(TransportBindingElement), element.BindingElementType, ref existingElement);
        }

        private void OnApplyConfiguration(Binding binding)
        {
            CustomBinding theBinding = (CustomBinding)binding;
            foreach (BindingElementExtensionElement bindingConfig in this)
            {
                theBinding.Elements.Add(bindingConfig.CreateBindingElement());
            }
        }

        public Binding CreateBinding()
        {
            CustomBinding customBinding = new CustomBinding();
            this.ApplyConfiguration(customBinding);
            return customBinding;
        }
        
        //protected override object GetElementKey(ConfigurationElement element) => throw new NotImplementedException();
    }
}
