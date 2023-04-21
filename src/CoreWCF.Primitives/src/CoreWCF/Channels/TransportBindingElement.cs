// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Security;
using WsdlNS = System.Web.Services.Description;

namespace CoreWCF.Channels
{
    public abstract class TransportBindingElement : BindingElement
    {
        private bool _manualAddressing;
        private long _maxBufferPoolSize;
        private long _maxReceivedMessageSize;

        protected TransportBindingElement()
        {
            _manualAddressing = TransportDefaults.ManualAddressing;
            _maxBufferPoolSize = TransportDefaults.MaxBufferPoolSize;
            _maxReceivedMessageSize = TransportDefaults.MaxReceivedMessageSize;
        }

        protected TransportBindingElement(TransportBindingElement elementToBeCloned)
        {
            _manualAddressing = elementToBeCloned._manualAddressing;
            _maxBufferPoolSize = elementToBeCloned._maxBufferPoolSize;
            _maxReceivedMessageSize = elementToBeCloned._maxReceivedMessageSize;
        }

        [System.ComponentModel.DefaultValueAttribute(false)]
        public virtual bool ManualAddressing
        {
            get
            {
                return _manualAddressing;
            }

            set
            {
                _manualAddressing = value;
            }
        }

        public virtual long MaxBufferPoolSize
        {
            get
            {
                return _maxBufferPoolSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.ValueMustBeNonNegative));
                }
                _maxBufferPoolSize = value;
            }
        }

        [System.ComponentModel.DefaultValueAttribute((long)65536)]
        public virtual long MaxReceivedMessageSize
        {
            get
            {
                return _maxReceivedMessageSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.ValueMustBePositive));
                }
                _maxReceivedMessageSize = value;
            }
        }

        public abstract string Scheme { get; }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ChannelProtectionRequirements))
            {
                ChannelProtectionRequirements myRequirements = GetProtectionRequirements(context);
                myRequirements.Add(context.GetInnerProperty<ChannelProtectionRequirements>() ?? new ChannelProtectionRequirements());
                return (T)(object)myRequirements;
            }

            // to cover all our bases, let's iterate through the BindingParameters to make sure
            // we haven't missed a query (since we're the Transport and we're at the bottom)
            Collection<BindingElement> bindingElements = new Collection<BindingElement>();
            foreach (object param in context.BindingParameters)
            {
                if (param is BindingElement element)
                {
                    bindingElements.Add(element);
                }
            }

            for (int i = 0; i < bindingElements.Count; i++)
            {
                T result = bindingElements[i].GetIndividualProperty<T>();
                if (result != default(T))
                {
                    return result;
                }
            }

            if (typeof(T) == typeof(MessageVersion))
            {
                return (T)(object)TransportDefaults.GetDefaultMessageEncoderFactory().MessageVersion;
            }

            if (typeof(T) == typeof(XmlDictionaryReaderQuotas))
            {
                return (T)(object)new XmlDictionaryReaderQuotas();
            }

            return null;
        }

        private ChannelProtectionRequirements GetProtectionRequirements(AddressingVersion addressingVersion)
        {
            ChannelProtectionRequirements result = new ChannelProtectionRequirements();
            result.IncomingSignatureParts.AddParts(addressingVersion.SignedMessageParts);
            result.OutgoingSignatureParts.AddParts(addressingVersion.SignedMessageParts);
            return result;
        }

        internal ChannelProtectionRequirements GetProtectionRequirements(BindingContext context)
        {
            AddressingVersion addressingVersion = AddressingVersion.WSAddressing10;
            MessageEncodingBindingElement messageEncoderBindingElement = context.Binding.Elements.Find<MessageEncodingBindingElement>();
            if (messageEncoderBindingElement != null)
            {
                addressingVersion = messageEncoderBindingElement.MessageVersion.Addressing;
            }

            return GetProtectionRequirements(addressingVersion);
        }

        public static void ExportWsdlEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext endpointContext,
            string wsdlTransportUri, EndpointAddress address, AddressingVersion addressingVersion)
        {
            if (exporter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            }

            if (endpointContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointContext));
            }

            // Set SoapBinding Transport URI
            if (wsdlTransportUri != null)
            {
                WsdlNS.SoapBinding soapBinding = SoapHelper.GetOrCreateSoapBinding(endpointContext, exporter);

                if (soapBinding != null)
                {
                    soapBinding.Transport = wsdlTransportUri;
                }
            }

            if (endpointContext.WsdlPort != null)
            {
                WsdlExporter.WSAddressingHelper.AddAddressToWsdlPort(endpointContext.WsdlPort, address, addressingVersion);
            }
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (b == null)
            {
                return false;
            }
            if (!(b is TransportBindingElement transport))
            {
                return false;
            }
            if (_maxBufferPoolSize != transport.MaxBufferPoolSize)
            {
                return false;
            }
            if (_maxReceivedMessageSize != transport.MaxReceivedMessageSize)
            {
                return false;
            }
            return true;
        }
    }
}
