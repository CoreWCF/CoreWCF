// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class UnixDomainSocketBinding : Binding
    {
        // private BindingElements
        private UnixDomainSocketTransportBindingElement _transport;
        private BinaryMessageEncodingBindingElement _encoding;
        private UnixDomainSocketSecurity _security = new UnixDomainSocketSecurity();

        public UnixDomainSocketBinding() { Initialize(); }
        public UnixDomainSocketBinding(UnixDomainSocketSecurityMode securityMode)
            : this()
        {
            _security.Mode = securityMode;
        }

        public TransferMode TransferMode
        {
            get { return _transport.TransferMode; }
            set { _transport.TransferMode = value; }
        }

        public HostNameComparisonMode HostNameComparisonMode
        {
            get { return _transport.HostNameComparisonMode; }
            set { _transport.HostNameComparisonMode = value; }
        }

        public long MaxReceivedMessageSize
        {
            get { return _transport.MaxReceivedMessageSize; }
            set { _transport.MaxReceivedMessageSize = value; }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return _encoding.ReaderQuotas; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                value.CopyTo(_encoding.ReaderQuotas);
            }
        }

        public override string Scheme { get { return _transport.Scheme; } }

        public EnvelopeVersion EnvelopeVersion
        {
            get { return EnvelopeVersion.Soap12; }
        }

        public UnixDomainSocketSecurity Security
        {
            get { return _security; }
            set
            {
                _security = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        private void Initialize()
        {
            _transport = new UnixDomainSocketTransportBindingElement();
            _encoding = new BinaryMessageEncodingBindingElement();
        }

        public override BindingElementCollection CreateBindingElements()
        {
            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection
            {
                // order of BindingElements is important
                // add encoding
                _encoding
            };
           
            // add transport security
            BindingElement transportSecurity = CreateTransportSecurity();
            if (transportSecurity != null)
            {
                bindingElements.Add(transportSecurity);
            }
            bindingElements.Add(_transport);
            return bindingElements.Clone();
        }

        private BindingElement CreateTransportSecurity()
        {
            return _security.CreateTransportSecurity();
        }
    }
}
