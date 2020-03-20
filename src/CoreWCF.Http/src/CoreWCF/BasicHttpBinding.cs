using System;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class BasicHttpBinding : HttpBindingBase
    {
        private BasicHttpSecurity _basicHttpSecurity;

        public BasicHttpBinding() : this(BasicHttpSecurityMode.None) { }

        public BasicHttpBinding(BasicHttpSecurityMode securityMode)
            : base()
        {
            Initialize();
            _basicHttpSecurity.Mode = securityMode;
        }

        internal WSMessageEncoding MessageEncoding { get; set; } = BasicHttpBindingDefaults.MessageEncoding;

        internal override BasicHttpSecurity BasicHttpSecurity => _basicHttpSecurity;

        public override BindingElementCollection CreateBindingElements()
        {
            CheckSettings();

            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            
            // order of BindingElements is important
            // add encoding
            if (MessageEncoding == WSMessageEncoding.Text)
                bindingElements.Add(TextMessageEncodingBindingElement);
            // add transport (http or https)
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }

        void Initialize()
        {
            _basicHttpSecurity = new BasicHttpSecurity();
        }
    }
}