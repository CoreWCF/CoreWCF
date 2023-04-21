// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.Versioning;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    [SupportedOSPlatform("windows")]
    public class NetNamedPipeBinding : Binding
    {
        // private BindingElements
        //TransactionFlowBindingElement context;
        private BinaryMessageEncodingBindingElement _encoding;
        private NamedPipeTransportBindingElement _namedPipe;
        private NetNamedPipeSecurity _security = new NetNamedPipeSecurity();

        public NetNamedPipeBinding()
            : base()
        {
            Initialize();
        }

        public NetNamedPipeBinding(NetNamedPipeSecurityMode securityMode)
            : this()
        {
            _security.Mode = securityMode;
        }

        private NetNamedPipeBinding(NetNamedPipeSecurity security)
            : this()
        {
            _security = security;
        }

        // The following properties have moved to NamedPipeListenOptions as that's the
        // equivalent of the shared listeners from WCF
        // ConnectionBufferSize
        // HostNameComparisonMode
        // MaxPendingAccepts

        //[DefaultValue(TransactionFlowDefaults.Transactions)]
        //public bool TransactionFlow
        //{
        //    get { return context.Transactions; }
        //    set { context.Transactions = value; }
        //}

        //public TransactionProtocol TransactionProtocol
        //{
        //    get { return this.context.TransactionProtocol; }
        //    set { this.context.TransactionProtocol = value; }
        //}

        [DefaultValue(ConnectionOrientedTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get { return _namedPipe.TransferMode; }
            set { _namedPipe.TransferMode = value; }
        }

        [DefaultValue(TransportDefaults.MaxBufferPoolSize)]
        public long MaxBufferPoolSize
        {
            get { return _namedPipe.MaxBufferPoolSize; }
            set
            {
                _namedPipe.MaxBufferPoolSize = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get { return _namedPipe.MaxBufferSize; }
            set { _namedPipe.MaxBufferSize = value; }
        }

        public int MaxConnections
        {
            get { return _namedPipe.MaxPendingConnections; }
            set
            {
                _namedPipe.MaxPendingConnections = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxReceivedMessageSize)]
        public long MaxReceivedMessageSize
        {
            get { return _namedPipe.MaxReceivedMessageSize; }
            set { _namedPipe.MaxReceivedMessageSize = value; }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return _encoding.ReaderQuotas; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                value.CopyTo(_encoding.ReaderQuotas);
            }
        }

        public override string Scheme { get { return _namedPipe.Scheme; } }

        public EnvelopeVersion EnvelopeVersion
        {
            get { return EnvelopeVersion.Soap12; }
        }

        public NetNamedPipeSecurity Security
        {
            get { return _security; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                _security = value;
            }
        }

        private void Initialize()
        {
            _namedPipe = new NamedPipeTransportBindingElement();
            _encoding = new BinaryMessageEncodingBindingElement();
            //context = GetDefaultTransactionFlowBindingElement();
        }

        public override BindingElementCollection CreateBindingElements()
        {   // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // add context
            //bindingElements.Add(context);
            // add encoding
            bindingElements.Add(_encoding);
            // add transport security
            WindowsStreamSecurityBindingElement transportSecurity = CreateTransportSecurity();
            if (transportSecurity != null)
            {
                bindingElements.Add(transportSecurity);
            }
            // add transport (named pipes)
            bindingElements.Add(_namedPipe);

            return bindingElements.Clone();
        }

        private WindowsStreamSecurityBindingElement CreateTransportSecurity()
        {
            if (_security.Mode == NetNamedPipeSecurityMode.Transport)
            {
                return _security.CreateTransportSecurity();
            }
            else
            {
                return null;
            }
        }
    }
}
