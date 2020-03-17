using System;
using System.ComponentModel;
using System.Net;

namespace CoreWCF.Channels
{
    public class HttpTransportBindingElement : TransportBindingElement
    {
        private int _maxBufferSize;
        private bool _maxBufferSizeInitialized;
        private string _realm;
        private TransferMode _transferMode;
        private WebSocketTransportSettings _webSocketSettings;

        //HttpAnonymousUriPrefixMatcher _anonymousUriPrefixMatcher;

        public HttpTransportBindingElement()
        {
            AuthenticationScheme = HttpTransportDefaults.AuthenticationScheme;
            MaxBufferSize = TransportDefaults.MaxBufferSize;
            KeepAliveEnabled = HttpTransportDefaults.KeepAliveEnabled;
            TransferMode = HttpTransportDefaults.TransferMode;
            WebSocketSettings = HttpTransportDefaults.GetDefaultWebSocketTransportSettings();
        }

        protected HttpTransportBindingElement(HttpTransportBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            AuthenticationScheme = elementToBeCloned.AuthenticationScheme;
            MaxBufferSize = elementToBeCloned.MaxBufferSize;
            KeepAliveEnabled = elementToBeCloned.KeepAliveEnabled;
            TransferMode = elementToBeCloned.TransferMode;
            WebSocketSettings = elementToBeCloned.WebSocketSettings.Clone();
        }
        // public bool AllowCookies { get { return default(bool); } set { } }
        public AuthenticationSchemes AuthenticationScheme { get; set; }

        // public System.Net.AuthenticationSchemes AuthenticationScheme { get { return default(System.Net.AuthenticationSchemes); } set { } }
        public int MaxBufferSize
        {
            get
            {
                if (_maxBufferSizeInitialized || TransferMode != TransferMode.Buffered)
                    return _maxBufferSize;

                long maxReceivedMessageSize = MaxReceivedMessageSize;
                if (maxReceivedMessageSize > int.MaxValue)
                    return int.MaxValue;
                else
                    return (int)maxReceivedMessageSize;
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
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _realm = value;
            }
        }

        public override string Scheme => "http";

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

        public WebSocketTransportSettings WebSocketSettings
        {
            get
            {
                return _webSocketSettings;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                _webSocketSettings = value;
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

        public override BindingElement Clone()
        {
            return new HttpTransportBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            //if (typeof(T) == typeof(ISecurityCapabilities))
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
            /*else*/ if (typeof(T) == typeof(TransferMode))
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
            //else if (typeof(T) == typeof(ITransportCompressionSupport))
            //{
            //    return (T)(object)new TransportCompressionSupportHelper();
            //}
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

            AuthenticationSchemes hostSupportedAuthenticationSchemes;

            if (!AuthenticationSchemesBindingParameter.TryExtract(bindingParameters, out hostSupportedAuthenticationSchemes))
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

        public override Type MiddlewareType => typeof(ServiceModelHttpMiddleware);
    }
}