using System.ComponentModel;
using System.Text;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class WebHttpBinding : Binding
    {
        private HttpsTransportBindingElement _httpsTransportBindingElement;
        // private BindingElements
        private HttpTransportBindingElement _httpTransportBindingElement;
        private WebHttpSecurity _security = new WebHttpSecurity();
        private WebMessageEncodingBindingElement _webMessageEncodingBindingElement;

        public WebHttpBinding() : base()
        {
            Initialize();
        }

        public WebHttpBinding(WebHttpSecurityMode securityMode) : base()
        {
            Initialize();
            _security.Mode = securityMode;
        }

        // This needs an update in the HttpTransportBindingElement to work.
        //[DefaultValue(false)]
        //public bool AllowCookies
        //{
        //    get { throw new PlatformNotSupportedException(); }
        //    set { throw new PlatformNotSupportedException(); }
        //}

        public EnvelopeVersion EnvelopeVersion => EnvelopeVersion.None;

        // This needs an update in the HttpTransportBindingElement to work.
        //[DefaultValue(HostNameComparisonMode.StrongWildcard)]
        //public HostNameComparisonMode HostNameComparisonMode
        //{
        //    get { throw new PlatformNotSupportedException(); }
        //    set { throw new PlatformNotSupportedException(); }
        //}

        [DefaultValue(TransportDefaults.MaxBufferPoolSize)]
        public long MaxBufferPoolSize
        {
            get { return _httpTransportBindingElement.MaxBufferPoolSize; }
            set
            {
                _httpTransportBindingElement.MaxBufferPoolSize = value;
                _httpsTransportBindingElement.MaxBufferPoolSize = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get { return _httpTransportBindingElement.MaxBufferSize; }
            set
            {
                _httpTransportBindingElement.MaxBufferSize = value;
                _httpsTransportBindingElement.MaxBufferSize = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxReceivedMessageSize)]
        public long MaxReceivedMessageSize
        {
            get { return _httpTransportBindingElement.MaxReceivedMessageSize; }
            set
            {
                _httpTransportBindingElement.MaxReceivedMessageSize = value;
                _httpsTransportBindingElement.MaxReceivedMessageSize = value;
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return _webMessageEncodingBindingElement.ReaderQuotas; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                value.CopyTo(_webMessageEncodingBindingElement.ReaderQuotas);
            }
        }

        public override string Scheme => GetTransport().Scheme;

        public WebHttpSecurity Security
        {
            get { return _security; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

                _security = value;
            }
        }

        [DefaultValue(HttpTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get { return _httpTransportBindingElement.TransferMode; }
            set
            {
                _httpTransportBindingElement.TransferMode = value;
                _httpsTransportBindingElement.TransferMode = value;
            }
        }

        [TypeConverter(typeof(EncodingConverter))]
        public Encoding WriteEncoding
        {
            get { return _webMessageEncodingBindingElement.WriteEncoding; }
            set
            {
                _webMessageEncodingBindingElement.WriteEncoding = value;
            }
        }

        public WebContentTypeMapper ContentTypeMapper
        {
            get { return _webMessageEncodingBindingElement.ContentTypeMapper; }
            set
            {
                _webMessageEncodingBindingElement.ContentTypeMapper = value;
            }
        }

        public bool CrossDomainScriptAccessEnabled
        {
            get { return _webMessageEncodingBindingElement.CrossDomainScriptAccessEnabled; }
            set
            {
                _webMessageEncodingBindingElement.CrossDomainScriptAccessEnabled = value;
            }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // add encoding 
            bindingElements.Add(_webMessageEncodingBindingElement);
            // add transport (http or https)
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }

        private TransportBindingElement GetTransport()
        {
            if (_security.Mode == WebHttpSecurityMode.Transport)
            {
                _security.EnableTransportSecurity(_httpsTransportBindingElement);
                return _httpsTransportBindingElement;
            }
            else if (_security.Mode == WebHttpSecurityMode.TransportCredentialOnly)
            {
                _security.EnableTransportAuthentication(_httpTransportBindingElement);
                return _httpTransportBindingElement;
            }
            else
            {
                // ensure that there is no transport security
                _security.DisableTransportAuthentication(_httpTransportBindingElement);
                return _httpTransportBindingElement;
            }
        }

        private void Initialize()
        {
            _httpTransportBindingElement = new HttpTransportBindingElement
            {
                ManualAddressing = true
            };

            _httpsTransportBindingElement = new HttpsTransportBindingElement
            {
                ManualAddressing = true
            };

            _webMessageEncodingBindingElement = new WebMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.None
            };
        }
    }
}
