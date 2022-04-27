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
                SetIsModified();
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
                SetIsModified();
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
                SetIsModified();
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
                SetIsModified();
            }
        }

        public override void Add(BindingElementExtensionElement element)
        {
            if (null == element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            BindingElementExtensionElement existingElement = null;
            if (!CanAddEncodingElement(element, ref existingElement))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigMessageEncodingAlreadyInBinding,
                    existingElement.ConfigurationElementName,
                    existingElement.GetType().AssemblyQualifiedName)));
            }
            else if (!CanAddStreamUpgradeElement(element, ref existingElement))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigStreamUpgradeElementAlreadyInBinding,
                    existingElement.ConfigurationElementName,
                    existingElement.GetType().AssemblyQualifiedName)));
            }
            else if (!CanAddTransportElement(element, ref existingElement))
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }
            if (binding.GetType() != typeof(CustomBinding))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.ConfigInvalidTypeForBinding,
                    typeof(CustomBinding).AssemblyQualifiedName,
                    binding.GetType().AssemblyQualifiedName));
            }

            binding.Name = Name;
            binding.CloseTimeout = CloseTimeout;
            binding.OpenTimeout = OpenTimeout;
            binding.ReceiveTimeout = ReceiveTimeout;
            binding.SendTimeout = SendTimeout;

            OnApplyConfiguration(binding);
        }

        public override bool CanAdd(BindingElementExtensionElement element)
        {
            if (null == element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            BindingElementExtensionElement existingElement = null;
            return !ContainsKey(element.GetType()) && CanAddEncodingElement(element, ref existingElement) &&
                CanAddStreamUpgradeElement(element, ref existingElement) && CanAddTransportElement(element, ref existingElement);
        }

        private bool CanAddEncodingElement(BindingElementExtensionElement element, ref BindingElementExtensionElement existingElement)
        {
            return CanAddExclusiveElement(typeof(MessageEncodingBindingElement), element.BindingElementType, ref existingElement);
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
            return CanAddExclusiveElement(typeof(StreamUpgradeBindingElement), element.BindingElementType, ref existingElement);
        }

        private bool CanAddTransportElement(BindingElementExtensionElement element, ref BindingElementExtensionElement existingElement)
        {
            return CanAddExclusiveElement(typeof(TransportBindingElement), element.BindingElementType, ref existingElement);
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
            ApplyConfiguration(customBinding);
            return customBinding;
        }
        
        //protected override object GetElementKey(ConfigurationElement element) => throw new NotImplementedException();
    }
}
