// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public class NetHttpBinding : HttpBindingBase
    {
        private BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement;
        private BasicHttpSecurity _basicHttpSecurity;

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
                _basicHttpSecurity = value ?? throw Fx.Exception.ArgumentNull(nameof(value));
            }
        }

        public WebSocketTransportSettings WebSocketSettings => InternalWebSocketSettings;

        internal override BasicHttpSecurity BasicHttpSecurity => _basicHttpSecurity;

        internal override void SetReaderQuotas(XmlDictionaryReaderQuotas readerQuotas)
        {
            readerQuotas.CopyTo(_binaryMessageEncodingBindingElement.ReaderQuotas);
        }

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
                    bindingElements.Add(MtomMessageEncodingBindingElement);
                    break;
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

        private void Initialize()
        {
            MessageEncoding = NetHttpBindingDefaults.MessageEncoding;
            _binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement() { MessageVersion = MessageVersion.Soap12WSAddressing10 };
            TextMessageEncodingBindingElement.MessageVersion = MessageVersion.Soap12WSAddressing10;
            MtomMessageEncodingBindingElement.MessageVersion = MessageVersion.Soap12WSAddressing10;
            WebSocketSettings.TransportUsage = NetHttpBindingDefaults.TransportUsage;
            WebSocketSettings.SubProtocol = WebSocketTransportSettings.SoapSubProtocol;
            _basicHttpSecurity = new BasicHttpSecurity();
        }
    }
}
