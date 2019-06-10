using System;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF
{
    public class BasicHttpBinding : HttpBindingBase
    {
        private WSMessageEncoding _messageEncoding = BasicHttpBindingDefaults.MessageEncoding;

        //public BasicHttpBinding(System.ServiceModel.BasicHttpSecurityMode securityMode) { }
        //public System.ServiceModel.BasicHttpSecurity Security { get { return default(CoreWCF.BasicHttpSecurity); } set { } }
        //public override CoreWCF.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(CoreWCF.Channels.BindingParameterCollection parameters) { return default(CoreWCF.Channels.IChannelFactory<TChannel>); }

        internal WSMessageEncoding MessageEncoding
        {
            get { return _messageEncoding; }
            set { _messageEncoding = value; }
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