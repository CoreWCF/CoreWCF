// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using WsdlNS = System.Web.Services.Description;
using CoreWCF.Description;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    // TODO: Consider moving to primitives
    public abstract class ConnectionOrientedTransportBindingElement : TransportBindingElement, IWsdlExportExtension, IPolicyExportExtension
    {
        private int _connectionBufferSize;
        private readonly bool _exposeConnectionProperty;
        private HostNameComparisonMode _hostNameComparisonMode;
        private TimeSpan _channelInitializationTimeout;
        private int _maxBufferSize;
        private bool _maxBufferSizeInitialized;
        private int _maxPendingConnections;
        private TimeSpan _maxOutputDelay;
        private int _maxPendingAccepts;
        private TransferMode _transferMode;

        internal ConnectionOrientedTransportBindingElement()
            : base()
        {
            _connectionBufferSize = ConnectionOrientedTransportDefaults.ConnectionBufferSize;
            _hostNameComparisonMode = ConnectionOrientedTransportDefaults.HostNameComparisonMode;
            _channelInitializationTimeout = ConnectionOrientedTransportDefaults.ChannelInitializationTimeout;
            _maxBufferSize = TransportDefaults.MaxBufferSize;
            _maxPendingConnections = ConnectionOrientedTransportDefaults.GetMaxPendingConnections();
            _maxOutputDelay = ConnectionOrientedTransportDefaults.MaxOutputDelay;
            _maxPendingAccepts = ConnectionOrientedTransportDefaults.GetMaxPendingAccepts();
            _transferMode = ConnectionOrientedTransportDefaults.TransferMode;
        }

        internal ConnectionOrientedTransportBindingElement(ConnectionOrientedTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _connectionBufferSize = elementToBeCloned._connectionBufferSize;
            _exposeConnectionProperty = elementToBeCloned._exposeConnectionProperty;
            _hostNameComparisonMode = elementToBeCloned._hostNameComparisonMode;
            InheritBaseAddressSettings = elementToBeCloned.InheritBaseAddressSettings;
            _channelInitializationTimeout = elementToBeCloned.ChannelInitializationTimeout;
            _maxBufferSize = elementToBeCloned._maxBufferSize;
            _maxBufferSizeInitialized = elementToBeCloned._maxBufferSizeInitialized;
            _maxPendingConnections = elementToBeCloned._maxPendingConnections;
            _maxOutputDelay = elementToBeCloned._maxOutputDelay;
            _maxPendingAccepts = elementToBeCloned._maxPendingAccepts;
            _transferMode = elementToBeCloned._transferMode;
            IsMaxPendingConnectionsSet = elementToBeCloned.IsMaxPendingConnectionsSet;
            IsMaxPendingAcceptsSet = elementToBeCloned.IsMaxPendingAcceptsSet;
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.ConnectionBufferSize)]
        public int ConnectionBufferSize
        {
            get
            {
                return _connectionBufferSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBeNonNegative));
                }

                _connectionBufferSize = value;
            }
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.HostNameComparisonMode)]
        public HostNameComparisonMode HostNameComparisonMode
        {
            get
            {
                return _hostNameComparisonMode;
            }

            set
            {
                HostNameComparisonModeHelper.Validate(value);
                _hostNameComparisonMode = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get
            {
                if (_maxBufferSizeInitialized || TransferMode != TransferMode.Buffered)
                {
                    return _maxBufferSize;
                }

                long maxReceivedMessageSize = MaxReceivedMessageSize;
                if (maxReceivedMessageSize > int.MaxValue)
                {
                    return int.MaxValue;
                }
                else
                {
                    return (int)maxReceivedMessageSize;
                }
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }

                _maxBufferSizeInitialized = true;
                _maxBufferSize = value;
            }
        }

        public int MaxPendingConnections
        {
            get
            {
                return _maxPendingConnections;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }

                _maxPendingConnections = value;
                IsMaxPendingConnectionsSet = true;
            }
        }

        internal bool IsMaxPendingConnectionsSet { get; private set; }

        // used by MEX to ensure that we don't conflict on base-address scoped settings
        internal bool InheritBaseAddressSettings { get; set; }

        public TimeSpan ChannelInitializationTimeout
        {
            get
            {
                return _channelInitializationTimeout;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                _channelInitializationTimeout = value;
            }
        }

        public TimeSpan MaxOutputDelay
        {
            get
            {
                return _maxOutputDelay;
            }

            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRange0));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                _maxOutputDelay = value;
            }
        }

        public int MaxPendingAccepts
        {
            get
            {
                return _maxPendingAccepts;
            }

            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }

                _maxPendingAccepts = value;
                IsMaxPendingAcceptsSet = true;
            }
        }

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            return true;
        }

        internal bool IsMaxPendingAcceptsSet { get; private set; }

        [DefaultValue(ConnectionOrientedTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get
            {
                return _transferMode;
            }
            set
            {
                TransferModeHelper.Validate(value);
                _transferMode = value;
            }
        }

        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            if (exporter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            }

            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            ICollection<XmlElement> policyAssertions = context.GetBindingAssertions();
            if (TransferModeHelper.IsRequestStreamed(TransferMode)
                || TransferModeHelper.IsResponseStreamed(TransferMode))
            {
                policyAssertions.Add(new XmlDocument().CreateElement(TransportPolicyConstants.DotNetFramingPrefix,
                    TransportPolicyConstants.StreamedName, TransportPolicyConstants.DotNetFramingNamespace));
            }

            bool createdNew;
            MessageEncodingBindingElement encodingBindingElement = FindMessageEncodingBindingElement(context.BindingElements, out createdNew);
            if (createdNew && encodingBindingElement is IPolicyExportExtension)
            {
                encodingBindingElement = new BinaryMessageEncodingBindingElement();
                ((IPolicyExportExtension)encodingBindingElement).ExportPolicy(exporter, context);
            }

            WsdlExporter.AddWSAddressingAssertion(exporter, context, encodingBindingElement.MessageVersion.Addressing);
        }

        void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext context) { }

        internal abstract string WsdlTransportUri { get; }

        void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext endpointContext)
        {
            bool createdNew;
            MessageEncodingBindingElement encodingBindingElement = FindMessageEncodingBindingElement(endpointContext, out createdNew);
            ExportWsdlEndpoint(exporter, endpointContext, WsdlTransportUri, encodingBindingElement.MessageVersion.Addressing);
        }

        private MessageEncodingBindingElement FindMessageEncodingBindingElement(BindingElementCollection bindingElements, out bool createdNew)
        {
            createdNew = false;
            MessageEncodingBindingElement encodingBindingElement = bindingElements.Find<MessageEncodingBindingElement>();
            if (encodingBindingElement == null)
            {
                createdNew = true;
                encodingBindingElement = new BinaryMessageEncodingBindingElement();
            }
            return encodingBindingElement;
        }

        private MessageEncodingBindingElement FindMessageEncodingBindingElement(WsdlEndpointConversionContext endpointContext, out bool createdNew)
        {
            BindingElementCollection bindingElements = endpointContext.Endpoint.Binding.CreateBindingElements();
            return FindMessageEncodingBindingElement(bindingElements, out createdNew);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(TransferMode))
            {
                return (T)(object)TransferMode;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (!base.IsMatch(b))
            {
                return false;
            }

            if (!(b is ConnectionOrientedTransportBindingElement connection))
            {
                return false;
            }

            if (_connectionBufferSize != connection._connectionBufferSize)
            {
                return false;
            }

            if (_hostNameComparisonMode != connection._hostNameComparisonMode)
            {
                return false;
            }

            if (InheritBaseAddressSettings != connection.InheritBaseAddressSettings)
            {
                return false;
            }

            if (_channelInitializationTimeout != connection._channelInitializationTimeout)
            {
                return false;
            }
            if (_maxBufferSize != connection._maxBufferSize)
            {
                return false;
            }

            if (_maxPendingConnections != connection._maxPendingConnections)
            {
                return false;
            }

            if (_maxOutputDelay != connection._maxOutputDelay)
            {
                return false;
            }

            if (_maxPendingAccepts != connection._maxPendingAccepts)
            {
                return false;
            }

            if (_transferMode != connection._transferMode)
            {
                return false;
            }

            return true;
        }

        internal static void ExportWsdlEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext endpointContext,
            string wsdlTransportUri, AddressingVersion addressingVersion)
        {
            ExportWsdlEndpoint(exporter, endpointContext, wsdlTransportUri, endpointContext.Endpoint.Address, addressingVersion);
        }
    }

    // Originally lived in TransportBindingElementImporter.cs
    internal static class TransportPolicyConstants
    {
        public const string BasicHttpAuthenticationName = "BasicAuthentication";
        public const string CompositeDuplex = "CompositeDuplex";
        public const string CompositeDuplexNamespace = "http://schemas.microsoft.com/net/2006/06/duplex";
        public const string CompositeDuplexPrefix = "cdp";
        public const string DigestHttpAuthenticationName = "DigestAuthentication";
        public const string DotNetFramingNamespace = Framing.FramingEncodingString.NamespaceUri + "/policy";
        public const string DotNetFramingPrefix = "msf";
        public const string HttpTransportNamespace = "http://schemas.microsoft.com/ws/06/2004/policy/http";
        public const string HttpTransportPrefix = "http";
        public const string HttpTransportUri = "http://schemas.xmlsoap.org/soap/http";
        public const string MsmqBestEffort = "MsmqBestEffort";
        public const string MsmqSession = "MsmqSession";
        public const string MsmqTransportNamespace = "http://schemas.microsoft.com/ws/06/2004/mspolicy/msmq";
        public const string MsmqTransportPrefix = "msmq";
        public const string MsmqTransportUri = "http://schemas.microsoft.com/soap/msmq";
        public const string MsmqVolatile = "MsmqVolatile";
        public const string MsmqAuthenticated = "Authenticated";
        public const string MsmqWindowsDomain = "WindowsDomain";
        public const string NamedPipeTransportUri = "http://schemas.microsoft.com/soap/named-pipe";
        public const string NegotiateHttpAuthenticationName = "NegotiateAuthentication";
        public const string NtlmHttpAuthenticationName = "NtlmAuthentication";
        public const string PeerTransportUri = "http://schemas.microsoft.com/soap/peer";
        public const string ProtectionLevelName = "ProtectionLevel";
        public const string RequireClientCertificateName = "RequireClientCertificate";
        public const string SslTransportSecurityName = "SslTransportSecurity";
        public const string StreamedName = "Streamed";
        public const string TcpTransportUri = "http://schemas.microsoft.com/soap/tcp";
        public const string WebSocketPolicyPrefix = "mswsp";
        public const string WebSocketPolicyNamespace = "http://schemas.microsoft.com/soap/websocket/policy";
        public const string WebSocketTransportUri = "http://schemas.microsoft.com/soap/websocket";
        public const string WebSocketEnabled = "WebSocketEnabled";
        public const string WindowsTransportSecurityName = "WindowsTransportSecurity";
    }
}
