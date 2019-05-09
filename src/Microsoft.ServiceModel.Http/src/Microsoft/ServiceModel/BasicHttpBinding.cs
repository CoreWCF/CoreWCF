using System;
using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Description;

namespace Microsoft.ServiceModel
{
    public class BasicHttpBinding : HttpBindingBase
    {
        private WSMessageEncoding _messageEncoding = BasicHttpBindingDefaults.MessageEncoding;

        //public BasicHttpBinding(System.ServiceModel.BasicHttpSecurityMode securityMode) { }
        //public System.ServiceModel.BasicHttpSecurity Security { get { return default(Microsoft.ServiceModel.BasicHttpSecurity); } set { } }
        //public override Microsoft.ServiceModel.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(Microsoft.ServiceModel.Channels.BindingParameterCollection parameters) { return default(Microsoft.ServiceModel.Channels.IChannelFactory<TChannel>); }

        internal WSMessageEncoding MessageEncoding
        {
            get { return _messageEncoding; }
            set { _messageEncoding = value; }
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(Uri listenUriBaseAddress,
            string listenUriRelativeAddress, ListenUriMode listenUriMode, BindingParameterCollection parameters)
        {
            throw new PlatformNotSupportedException("BuildChannelListener not supported for HTTP");
        }

        public override bool CanBuildChannelListener<TChannel>(BindingParameterCollection parameters)
        {
            return false;
        }

        public override BindingElementCollection CreateBindingElements()
        {
            CheckSettings();

            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            
            // order of BindingElements is important
            // add security (*optional)
            //SecurityBindingElement wsSecurity = BasicHttpSecurity.CreateMessageSecurity();
            //if (wsSecurity != null)
            //{
            //    bindingElements.Add(wsSecurity);
            //}
            // add encoding
            if (MessageEncoding == WSMessageEncoding.Text)
                bindingElements.Add(TextMessageEncodingBindingElement);
            // add transport (http or https)
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }
    }
}