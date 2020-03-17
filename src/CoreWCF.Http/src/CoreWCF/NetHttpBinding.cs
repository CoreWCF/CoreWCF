using System;
using System.ComponentModel;
using System.Runtime;
using CoreWCF.Channels;
using System.Text;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public class NetHttpBinding : HttpBindingBase
    {
        BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement;
        BasicHttpSecurity _basicHttpSecurity;

        public NetHttpBinding()
            : this(BasicHttpSecurityMode.None)
        {
        }

        public NetHttpBinding(BasicHttpSecurityMode securityMode)
            : base()
        {
            Initialize();
            _basicHttpSecurity.Mode = securityMode;
        }

        public NetHttpMessageEncoding MessageEncoding { get; set; }

        public BasicHttpSecurity Security
        {
            get => _basicHttpSecurity;

            set
            {
                if (value == null)
                {
                    throw Fx.Exception.ArgumentNull(nameof(value));
                }

                _basicHttpSecurity = value;
            }
        }

        public WebSocketTransportSettings WebSocketSettings => InternalWebSocketSettings;

        internal override BasicHttpSecurity BasicHttpSecurity => _basicHttpSecurity;

        public override BindingElementCollection CreateBindingElements()
        {
            CheckSettings();

            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();

            // order of BindingElements is important
            // add encoding
            switch (MessageEncoding)
            {
                case NetHttpMessageEncoding.Text:
                    bindingElements.Add(TextMessageEncodingBindingElement);
                    break;
                case NetHttpMessageEncoding.Mtom:
                    throw new PlatformNotSupportedException(nameof(NetHttpMessageEncoding.Mtom));
                default:
                    bindingElements.Add(_binaryMessageEncodingBindingElement);
                    break;
            }

            // add transport (http or https)
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }

        internal override void CheckSettings()
        {
            base.CheckSettings();
        }

        void Initialize()
        {
            MessageEncoding = NetHttpBindingDefaults.MessageEncoding;
            _binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement() { MessageVersion = MessageVersion.Soap12WSAddressing10 };
            TextMessageEncodingBindingElement.MessageVersion = MessageVersion.Soap12WSAddressing10;
            WebSocketSettings.TransportUsage = NetHttpBindingDefaults.TransportUsage;
            WebSocketSettings.SubProtocol = WebSocketTransportSettings.SoapSubProtocol;
            _basicHttpSecurity = new BasicHttpSecurity();
        }
    }
}
