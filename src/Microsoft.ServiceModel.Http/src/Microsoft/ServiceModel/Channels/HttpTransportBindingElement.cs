using System;
using System.ComponentModel;
using System.Net;

namespace Microsoft.ServiceModel.Channels
{
    public class HttpTransportBindingElement : TransportBindingElement
    {
        //HttpAnonymousUriPrefixMatcher _anonymousUriPrefixMatcher;

        public HttpTransportBindingElement()
        {
            MaxBufferSize = TransportDefaults.MaxBufferSize;
            KeepAliveEnabled = HttpTransportDefaults.KeepAliveEnabled;
            TransferMode = HttpTransportDefaults.TransferMode;
        }

        protected HttpTransportBindingElement(HttpTransportBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            MaxBufferSize = elementToBeCloned.MaxBufferSize;
            TransferMode = elementToBeCloned.TransferMode;
        }
        // [System.ComponentModel.DefaultValueAttribute(false)]
        // public bool AllowCookies { get { return default(bool); } set { } }
        // [System.ComponentModel.DefaultValueAttribute((System.Net.AuthenticationSchemes)(32768))]
        // public System.Net.AuthenticationSchemes AuthenticationScheme { get { return default(System.Net.AuthenticationSchemes); } set { } }
        public int MaxBufferSize { get; set; }
        public bool KeepAliveEnabled { get; set; }
        public override string Scheme { get { return "http"; } }
        // [System.ComponentModel.DefaultValueAttribute((System.ServiceModel.TransferMode)(0))]
        public TransferMode TransferMode { get; set; }
        // public System.ServiceModel.Channels.WebSocketTransportSettings WebSocketSettings { get { return default(System.ServiceModel.Channels.WebSocketTransportSettings); } set { } }
        // public override System.ServiceModel.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(System.ServiceModel.Channels.BindingContext context) { return default(System.ServiceModel.Channels.IChannelFactory<TChannel>); }
        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IReplyChannel))
            {
                return true;
                //return this.WebSocketSettings.TransportUsage != WebSocketTransportUsage.Always;
            }
            //else if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            //{
            //    return this.WebSocketSettings.TransportUsage != WebSocketTransportUsage.Never;
            //}
            return false;
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
            }

            if (!CanBuildChannelListener<TChannel>(context))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    "TChannel", SR.Format(SR.CouldnTCreateChannelForChannelType2, context.Binding.Name, typeof(TChannel)));
            }

            return null;
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

        public override Type MiddlewareType => typeof(ServiceModelHttpMiddleware);
    }
}