// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication.ExtendedProtection;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using WsdlNS = System.Web.Services.Description;

namespace CoreWCF.Channels
{
    /// <summary>
    /// This TransportBindingElement is used to specify an HTTP transport for transmitting messages.
    /// </summary>
    /// <remarks>HttpTransportBindingElement is also used as a starting point for creating a custom bindings using HTTP.</remarks>
    public class HttpTransportBindingElement : TransportBindingElement, IWsdlExportExtension, IPolicyExportExtension
    {
        private int _maxBufferSize;
        private bool _maxBufferSizeInitialized;
        private string _realm;
        private TransferMode _transferMode;
        private WebSocketTransportSettings _webSocketSettings;
        private ExtendedProtectionPolicy _extendedProtectionPolicy;

        //HttpAnonymousUriPrefixMatcher _anonymousUriPrefixMatcher;

        /// <summary>
        /// Initializes a new instance of the HttpTransportBindingElement class.
        /// </summary>
        /// <remarks>The defaults are AuthenticationSchemes.Anonymous, TransferMode.Buffered, MaxBufferSize = 65536, and KeepAliveEnabled.</remarks>
        public HttpTransportBindingElement()
        {
            AuthenticationScheme = HttpTransportDefaults.AuthenticationScheme;
            _maxBufferSize = TransportDefaults.MaxBufferSize;
            KeepAliveEnabled = HttpTransportDefaults.KeepAliveEnabled;
            TransferMode = HttpTransportDefaults.TransferMode;
            WebSocketSettings = HttpTransportDefaults.GetDefaultWebSocketTransportSettings();
        }

        protected HttpTransportBindingElement(HttpTransportBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            AuthenticationScheme = elementToBeCloned.AuthenticationScheme;
            _maxBufferSize = elementToBeCloned._maxBufferSize;
            _maxBufferSizeInitialized = elementToBeCloned._maxBufferSizeInitialized;
            KeepAliveEnabled = elementToBeCloned.KeepAliveEnabled;
            TransferMode = elementToBeCloned.TransferMode;
            WebSocketSettings = elementToBeCloned.WebSocketSettings.Clone();
        }

        // public bool AllowCookies { get { return default(bool); } set { } }

        /// <summary>
        /// Gets or sets the authentication scheme.
        /// </summary>
        /// <value>The authentication scheme.</value>
        public AuthenticationSchemes AuthenticationScheme { get; set; }

        // public System.Net.AuthenticationSchemes AuthenticationScheme { get { return default(System.Net.AuthenticationSchemes); } set { } }

        /// <summary>
        /// Gets or sets the maximum size of the buffer.
        /// </summary>
        /// <value>The maximum size of the buffer.</value>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to 0.</exception>
        /// <remarks>If not set, this defaults the to lessor of MaxReceivedMessageSize and Int32.MaxValue.</remarks>
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

        public bool KeepAliveEnabled { get; set; }

        public string Realm
        {
            get
            {
                return _realm;
            }
            set
            {
                _realm = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets the HTTP scheme.
        /// </summary>
        /// <value>The HTTP scheme.</value>
        /// <remarks>This will be 'http' unless overridden in a subclass.</remarks>
        public override string Scheme => "http";

        /// <summary>
        /// Gets or sets the transfer mode.
        /// </summary>
        /// <value>The transfer mode.</value>
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

        /// <summary>
        /// Gets or sets the web socket settings.
        /// </summary>
        /// <value>The web socket settings. This may not be null.</value>
        public WebSocketTransportSettings WebSocketSettings
        {
            get
            {
                return _webSocketSettings;
            }
            set
            {
                _webSocketSettings = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        internal virtual bool GetSupportsClientAuthenticationImpl(AuthenticationSchemes effectiveAuthenticationSchemes)
        {
            return effectiveAuthenticationSchemes != AuthenticationSchemes.None &&
                effectiveAuthenticationSchemes.IsNotSet(AuthenticationSchemes.Anonymous);
        }

        internal virtual bool GetSupportsClientWindowsIdentityImpl(AuthenticationSchemes effectiveAuthenticationSchemes)
        {
            return effectiveAuthenticationSchemes != AuthenticationSchemes.None &&
                effectiveAuthenticationSchemes.IsNotSet(AuthenticationSchemes.Anonymous);
        }

        internal string GetWsdlTransportUri(bool useWebSocketTransport)
        {
            if (useWebSocketTransport)
            {
                return TransportPolicyConstants.WebSocketTransportUri;
            }

            return TransportPolicyConstants.HttpTransportUri;
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        public override BindingElement Clone()
        {
            return new HttpTransportBindingElement(this);
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            IApplicationBuilder app = context.BindingParameters.Find<IApplicationBuilder>();
            if (app == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(IApplicationBuilder));
            }

            // Wire up inner dispatcher to ServiceModelHttpMiddleware so that incoming requests get dispatched
            //ServiceModelHttpMiddleware.ConfigureDispatcher(app, innerDispatcher);
            // Return the previous inner dispatcher as we don't create a wrapping dispatcher here.
            return innerDispatcher;
        }

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IReplyChannel))
            {
                return WebSocketSettings.TransportUsage != WebSocketTransportUsage.Always;
            }
            else if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                return WebSocketSettings.TransportUsage != WebSocketTransportUsage.Never;
            }

            return false;
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ITransportServiceBuilder))
            {
                return (T)(object)new HttpTransportServiceBuilder();
            }
            //else if (typeof(T) == typeof(ISecurityCapabilities))
            //{
            //    AuthenticationSchemes effectiveAuthenticationSchemes = HttpTransportBindingElement.GetEffectiveAuthenticationSchemes(this.AuthenticationScheme,
            //        context.BindingParameters);

            //    return (T)(object)new SecurityCapabilities(this.GetSupportsClientAuthenticationImpl(effectiveAuthenticationSchemes),
            //        effectiveAuthenticationSchemes == AuthenticationSchemes.Negotiate,
            //        this.GetSupportsClientWindowsIdentityImpl(effectiveAuthenticationSchemes),
            //        ProtectionLevel.None,
            //        ProtectionLevel.None);
            //}
            //else if (typeof(T) == typeof(IBindingDeliveryCapabilities))
            //{
            //    return (T)(object)new BindingDeliveryCapabilitiesHelper();
            //}
            else if (typeof(T) == typeof(TransferMode))
            {
                return (T)(object)TransferMode;
            }
            //else if (typeof(T) == typeof(ExtendedProtectionPolicy))
            //{
            //    return (T)(object)this.ExtendedProtectionPolicy;
            //}
            //else if (typeof(T) == typeof(IAnonymousUriPrefixMatcher))
            //{
            //    if (_anonymousUriPrefixMatcher == null)
            //    {
            //        _anonymousUriPrefixMatcher = new HttpAnonymousUriPrefixMatcher();
            //    }

            //    return (T)(object)_anonymousUriPrefixMatcher;
            //}
            else if (typeof(T).FullName.Equals("CoreWCF.Channels.ITransportCompressionSupport"))
            {
                IApplicationBuilder app = context.BindingParameters.Find<IApplicationBuilder>();
                if (app == null)
                {
                    return base.GetProperty<T>(context);
                }

                object tcs = app.ApplicationServices.GetService(typeof(T).Assembly.GetType("CoreWCF.Channels.TransportCompressionSupportHelper"));
                return (T)tcs;
            }
            else
            {
                if (context.BindingParameters.Find<MessageEncodingBindingElement>() == null)
                {
                    context.BindingParameters.Add(new TextMessageEncodingBindingElement());
                }
                return base.GetProperty<T>(context);
            }
        }

        internal static AuthenticationSchemes GetEffectiveAuthenticationSchemes(AuthenticationSchemes currentAuthenticationSchemes, BindingParameterCollection bindingParameters)
        {
            if (bindingParameters == null)
            {
                return currentAuthenticationSchemes;
            }

            if (!AuthenticationSchemesBindingParameter.TryExtract(bindingParameters, out AuthenticationSchemes hostSupportedAuthenticationSchemes))
            {
                return currentAuthenticationSchemes;
            }

            // TODO: Add logic for Metadata endpoints to inherit authentication scheme of host. This might not be necessary, needs more thought.
            //if (currentAuthenticationSchemes == AuthenticationSchemes.None ||
            //    (AspNetEnvironment.Current.IsMetadataListener(bindingParameters) &&
            //    currentAuthenticationSchemes == AuthenticationSchemes.Anonymous &&
            //    hostSupportedAuthenticationSchemes.IsNotSet(AuthenticationSchemes.Anonymous)))
            //{
            //    //Inherit authentication schemes from host.
            //    //This logic of inheriting from the host for anonymous MEX endpoints was previously implemented in HostedAspNetEnvironment.ValidateHttpSettings.
            //    //We moved it here to maintain the pre-multi-auth behavior. (see CSDMain 183553)

            //    if (!hostSupportedAuthenticationSchemes.IsSingleton() &&
            //         hostSupportedAuthenticationSchemes.IsSet(AuthenticationSchemes.Anonymous) &&
            //         AspNetEnvironment.Current.AspNetCompatibilityEnabled &&
            //         AspNetEnvironment.Current.IsSimpleApplicationHost &&
            //         AspNetEnvironment.Current.IsWindowsAuthenticationConfigured())
            //    {
            //        // Remove Anonymous if ASP.Net authentication mode is Windows (Asp.Net would not allow anonymous requests in this case anyway)
            //        hostSupportedAuthenticationSchemes ^= AuthenticationSchemes.Anonymous;
            //    }

            //    return hostSupportedAuthenticationSchemes;
            //}
            //else
            //{
            //build intersection between AuthenticationSchemes supported on the HttpTransportbidningELement and ServiceHost/IIS
            return currentAuthenticationSchemes & hostSupportedAuthenticationSchemes;
            //}
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy
        {
            get
            {
                return _extendedProtectionPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.PolicyEnforcement == PolicyEnforcement.Always &&
                    !ExtendedProtectionPolicy.OSSupportsExtendedProtection)
                {
                    throw new PlatformNotSupportedException(SR.ExtendedProtectionNotSupported);
                }

                _extendedProtectionPolicy = value;
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

            OnExportPolicy(exporter, context);

            bool createdNew;
            MessageEncodingBindingElement encodingBindingElement = FindMessageEncodingBindingElement(context.BindingElements, out createdNew);
            if (createdNew && encodingBindingElement is IPolicyExportExtension)
            {
                ((IPolicyExportExtension)encodingBindingElement).ExportPolicy(exporter, context);
            }

            WsdlExporter.AddWSAddressingAssertion(exporter, context, encodingBindingElement.MessageVersion.Addressing);
        }

        internal virtual void OnExportPolicy(MetadataExporter exporter, PolicyConversionContext policyContext)
        {
            List<string> assertionNames = new List<string>();
            AuthenticationSchemes effectiveAuthenticationSchemes = HttpTransportBindingElement.GetEffectiveAuthenticationSchemes(AuthenticationScheme,
                    policyContext.BindingParameters);

            if (effectiveAuthenticationSchemes != AuthenticationSchemes.None && !(effectiveAuthenticationSchemes.IsSet(AuthenticationSchemes.Anonymous)))
            {
                // ATTENTION: The order of the if-statements below is essential! When importing WSDL svcutil is actually
                // using the first assertion - and the HTTP spec requires clients to use the most secure authentication
                // scheme supported by the client. (especially important for downlevel (3.5/4.0) clients
                if (effectiveAuthenticationSchemes.IsSet(AuthenticationSchemes.Negotiate))
                {
                    assertionNames.Add(TransportPolicyConstants.NegotiateHttpAuthenticationName);
                }

                if (effectiveAuthenticationSchemes.IsSet(AuthenticationSchemes.Ntlm))
                {
                    assertionNames.Add(TransportPolicyConstants.NtlmHttpAuthenticationName);
                }

                if (effectiveAuthenticationSchemes.IsSet(AuthenticationSchemes.Digest))
                {
                    assertionNames.Add(TransportPolicyConstants.DigestHttpAuthenticationName);
                }

                if (effectiveAuthenticationSchemes.IsSet(AuthenticationSchemes.Basic))
                {
                    assertionNames.Add(TransportPolicyConstants.BasicHttpAuthenticationName);
                }

                if (assertionNames.Count > 0)
                {
                    if (assertionNames.Count == 1)
                    {
                        policyContext.GetBindingAssertions().Add(new XmlDocument().CreateElement(TransportPolicyConstants.HttpTransportPrefix,
                            assertionNames[0], TransportPolicyConstants.HttpTransportNamespace));
                    }
                    else
                    {
                        XmlDocument dummy = new XmlDocument();
                        XmlElement root = dummy.CreateElement(MetadataStrings.WSPolicy.Prefix,
                            MetadataStrings.WSPolicy.Elements.ExactlyOne,
                            exporter.PolicyVersion.Namespace);

                        foreach (string assertionName in assertionNames)
                        {
                            root.AppendChild(dummy.CreateElement(TransportPolicyConstants.HttpTransportPrefix,
                                assertionName,
                                TransportPolicyConstants.HttpTransportNamespace));
                        }

                        policyContext.GetBindingAssertions().Add(root);
                    }
                }
            }

            bool useWebSocketTransport = WebSocketHelper.UseWebSocketTransport(WebSocketSettings.TransportUsage, policyContext.Contract.IsDuplex());
            if (useWebSocketTransport && TransferMode != TransferMode.Buffered)
            {
                policyContext.GetBindingAssertions().Add(new XmlDocument().CreateElement(TransportPolicyConstants.WebSocketPolicyPrefix,
                TransferMode.ToString(), TransportPolicyConstants.WebSocketPolicyNamespace));
            }
        }

        void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext context) { }

        void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext endpointContext)
        {
            bool createdNew;
            MessageEncodingBindingElement encodingBindingElement = FindMessageEncodingBindingElement(endpointContext, out createdNew);
            bool useWebSocketTransport = WebSocketHelper.UseWebSocketTransport(WebSocketSettings.TransportUsage, endpointContext.ContractConversionContext.Contract.IsDuplex());

            EndpointAddress address = endpointContext.Endpoint.Address;
            if (useWebSocketTransport)
            {
                address = new EndpointAddress(WebSocketHelper.GetWebSocketUri(endpointContext.Endpoint.Address.Uri), endpointContext.Endpoint.Address);
                WsdlNS.SoapAddressBinding binding = GetSoapAddressBinding(endpointContext.WsdlPort);
                if (binding != null)
                {
                    binding.Location = address.Uri.AbsoluteUri;
                }
            }

            ExportWsdlEndpoint(exporter, endpointContext, GetWsdlTransportUri(useWebSocketTransport), address, encodingBindingElement.MessageVersion.Addressing);
        }

        private static WsdlNS.SoapAddressBinding GetSoapAddressBinding(WsdlNS.Port wsdlPort)
        {
            foreach (object o in wsdlPort.Extensions)
            {
                if (o is WsdlNS.SoapAddressBinding binding)
                {
                    return binding;
                }
            }
            return null;
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
    }

    internal static class TransportPolicyConstants
    {
        public const string BasicHttpAuthenticationName = "BasicAuthentication";
        public const string CompositeDuplex = "CompositeDuplex";
        public const string CompositeDuplexNamespace = "http://schemas.microsoft.com/net/2006/06/duplex";
        public const string CompositeDuplexPrefix = "cdp";
        public const string DigestHttpAuthenticationName = "DigestAuthentication";
        public const string HttpTransportNamespace = "http://schemas.microsoft.com/ws/06/2004/policy/http";
        public const string HttpTransportPrefix = "http";
        public const string HttpTransportUri = "http://schemas.xmlsoap.org/soap/http";
        public const string NegotiateHttpAuthenticationName = "NegotiateAuthentication";
        public const string NtlmHttpAuthenticationName = "NtlmAuthentication";
        public const string ProtectionLevelName = "ProtectionLevel";
        public const string RequireClientCertificateName = "RequireClientCertificate";
        public const string SslTransportSecurityName = "SslTransportSecurity";
        public const string StreamedName = "Streamed";
        public const string WebSocketPolicyPrefix = "mswsp";
        public const string WebSocketPolicyNamespace = "http://schemas.microsoft.com/soap/websocket/policy";
        public const string WebSocketTransportUri = "http://schemas.microsoft.com/soap/websocket";
        public const string WebSocketEnabled = "WebSocketEnabled";
        public const string WindowsTransportSecurityName = "WindowsTransportSecurity";
    }
}
