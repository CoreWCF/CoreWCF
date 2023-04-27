// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Text;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class NetTcpBinding : Binding
    {
        private OptionalReliableSession _reliableSession;
        // private BindingElements
        private TcpTransportBindingElement _transport;
        private BinaryMessageEncodingBindingElement _encoding;
        private ReliableSessionBindingElement _session;
        private NetTcpSecurity _security = new NetTcpSecurity();

        public NetTcpBinding() { Initialize(); }
        public NetTcpBinding(SecurityMode securityMode)
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

        [DefaultValue(TransportDefaults.MaxBufferPoolSize)]
        public long MaxBufferPoolSize
        {
            get { return _transport.MaxBufferPoolSize; }
            set
            {
                _transport.MaxBufferPoolSize = value;
            }
        }

        public int MaxBufferSize
        {
            get { return _transport.MaxBufferSize; }
            set { _transport.MaxBufferSize = value; }
        }

        public int MaxConnections
        {
            get { return _transport.MaxPendingConnections; }
            set
            {
                _transport.MaxPendingConnections = value;
            }
        }

        public int ListenBacklog
        {
            get { return _transport.ListenBacklog; }
            set { _transport.ListenBacklog = value; }
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

        //TODO: Work out if we want IBindingRuntimePreferences. Probably not as we're aiming for 100% async here
        //bool IBindingRuntimePreferences.ReceiveSynchronously
        //{
        //    get { return false; }
        //}

        public OptionalReliableSession ReliableSession
        {
            get
            {
                return _reliableSession;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }

                _reliableSession.Ordered = value.Ordered;
                _reliableSession.InactivityTimeout = value.InactivityTimeout;
                _reliableSession.Enabled = value.Enabled;
            }
        }

        public override string Scheme { get { return _transport.Scheme; } }

        public EnvelopeVersion EnvelopeVersion
        {
            get { return EnvelopeVersion.Soap12; }
        }

        internal SecurityBindingElement CreateMessageSecurity()
        {
            if (Security.Mode == SecurityMode.Message || Security.Mode == SecurityMode.TransportWithMessageCredential)
            {
                return Security.CreateMessageSecurity(false);//ReliableSession.Enabled);
            }
            else
            {
                return null;
            }
        }

        public NetTcpSecurity Security
        {
            get { return _security; }
            set
            {
                _security = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        private void Initialize()
        {
            _transport = new TcpTransportBindingElement();
            _encoding = new BinaryMessageEncodingBindingElement();
            _session = new ReliableSessionBindingElement();
            _reliableSession = new OptionalReliableSession(_session);
        }

        public override BindingElementCollection CreateBindingElements()
        {
            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // add session
            if (_reliableSession.Enabled)
            {
                bindingElements.Add(_session);
            }
            // add security (*optional)
            SecurityBindingElement wsSecurity = CreateMessageSecurity();
            if (wsSecurity != null)
            {
                bindingElements.Add(wsSecurity);
            }
            // add encoding
            bindingElements.Add(_encoding);
            // add transport security
            BindingElement transportSecurity = CreateTransportSecurity();
            if (transportSecurity != null)
            {
                bindingElements.Add(transportSecurity);
            }
            // TODO: Add ExtendedProtectionPolicy
            _transport.ExtendedProtectionPolicy = _security.Transport.ExtendedProtectionPolicy;
            // add transport (tcp)
            bindingElements.Add(_transport);

            return bindingElements.Clone();
        }

        private BindingElement CreateTransportSecurity()
        {
            return _security.CreateTransportSecurity();
        }
    }
}
