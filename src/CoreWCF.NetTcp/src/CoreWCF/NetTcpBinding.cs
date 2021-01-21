// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class NetTcpBinding : Binding
    {
        // private BindingElements
        private TcpTransportBindingElement transport;
        private BinaryMessageEncodingBindingElement encoding;
        private NetTcpSecurity security = new NetTcpSecurity();

        public NetTcpBinding() { Initialize(); }
        public NetTcpBinding(SecurityMode securityMode)
            : this()
        {
            security.Mode = securityMode;
        }

        public TransferMode TransferMode
        {
            get { return transport.TransferMode; }
            set { transport.TransferMode = value; }
        }

        public HostNameComparisonMode HostNameComparisonMode
        {
            get { return transport.HostNameComparisonMode; }
            set { transport.HostNameComparisonMode = value; }
        }

        [DefaultValue(TransportDefaults.MaxBufferPoolSize)]
        public long MaxBufferPoolSize
        {
            get { return transport.MaxBufferPoolSize; }
            set
            {
                transport.MaxBufferPoolSize = value;
            }
        }

        public int MaxBufferSize
        {
            get { return transport.MaxBufferSize; }
            set { transport.MaxBufferSize = value; }
        }

        public int MaxConnections
        {
            get { return transport.MaxPendingConnections; }
            set
            {
                transport.MaxPendingConnections = value;
            }
        }

        internal bool IsMaxConnectionsSet
        {
            get { return transport.IsMaxPendingConnectionsSet; }
        }

        public int ListenBacklog
        {
            get { return transport.ListenBacklog; }
            set { transport.ListenBacklog = value; }
        }

        public long MaxReceivedMessageSize
        {
            get { return transport.MaxReceivedMessageSize; }
            set { transport.MaxReceivedMessageSize = value; }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return encoding.ReaderQuotas; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                value.CopyTo(encoding.ReaderQuotas);
            }
        }

        //TODO: Work out if we want IBindingRuntimePreferences. Probably not as we're aiming for 100% async here
        //bool IBindingRuntimePreferences.ReceiveSynchronously
        //{
        //    get { return false; }
        //}

        public override string Scheme { get { return transport.Scheme; } }

        public EnvelopeVersion EnvelopeVersion
        {
            get { return EnvelopeVersion.Soap12; }
        }

        public NetTcpSecurity Security
        {
            get { return security; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                security = value;
            }
        }

        private void Initialize()
        {
            transport = new TcpTransportBindingElement();
            encoding = new BinaryMessageEncodingBindingElement();
        }

        private void CheckSettings()
        {
            NetTcpSecurity security = Security;
            if (security == null)
            {
                return;
            }

            SecurityMode mode = security.Mode;
            if (mode == SecurityMode.None)
            {
                return;
            }
            else if (mode == SecurityMode.Message)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Mode", mode)));
            }

            // Message.ClientCredentialType = Certificate, IssuedToken or Windows are not supported.
            if (mode == SecurityMode.TransportWithMessageCredential)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Mode", mode)));
            }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            CheckSettings();

            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // add encoding
            bindingElements.Add(encoding);
            // add transport security
            BindingElement transportSecurity = CreateTransportSecurity();
            if (transportSecurity != null)
            {
                bindingElements.Add(transportSecurity);
            }
            // TODO: Add ExtendedProtectionPolicy
            transport.ExtendedProtectionPolicy = security.Transport.ExtendedProtectionPolicy;
            // add transport (tcp)
            bindingElements.Add(transport);

            return bindingElements.Clone();
        }

        private BindingElement CreateTransportSecurity()
        {
            return security.CreateTransportSecurity();
        }
    }
}
