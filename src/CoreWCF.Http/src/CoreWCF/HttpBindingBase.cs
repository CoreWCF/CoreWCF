using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public abstract class HttpBindingBase : Binding
    {
        private HttpTransportBindingElement _httpTransport;
        private HttpsTransportBindingElement _httpsTransport;
        private TextMessageEncodingBindingElement _textEncoding;

        internal HttpBindingBase()
        {
            _httpTransport = new HttpTransportBindingElement();
            _textEncoding = new TextMessageEncodingBindingElement();
            _textEncoding.MessageVersion = MessageVersion.Soap11;
        }
        // [System.ComponentModel.DefaultValueAttribute(false)]
        // public bool AllowCookies { get { return default(bool); } set { } }
        public EnvelopeVersion EnvelopeVersion { get { return default(EnvelopeVersion); } }
        // [System.ComponentModel.DefaultValueAttribute((long)524288)]
        // public long MaxBufferPoolSize { get { return default(long); } set { } }
        // [System.ComponentModel.DefaultValueAttribute(65536)]
        // public int MaxBufferSize { get { return default(int); } set { } }
        public long MaxReceivedMessageSize { get { return default(long); } set { } }
        public XmlDictionaryReaderQuotas ReaderQuotas { get { return default(XmlDictionaryReaderQuotas); } set { } }

        public override string Scheme { get { return GetTransport().Scheme; } }
        public Encoding TextEncoding { get { return default(Encoding); } set { } }
        // [System.ComponentModel.DefaultValueAttribute((System.ServiceModel.TransferMode)(0))]
        // public System.ServiceModel.TransferMode TransferMode { get { return default(System.ServiceModel.TransferMode); } set { } }

        internal TextMessageEncodingBindingElement TextMessageEncodingBindingElement
        {
            get
            {
                return _textEncoding;
            }
        }

        internal abstract BasicHttpSecurity BasicHttpSecurity
        {
            get;
        }

        internal WebSocketTransportSettings InternalWebSocketSettings
        {
            get
            {
                return _httpTransport.WebSocketSettings;
            }
        }

        internal TransportBindingElement GetTransport()
        {
            Fx.Assert(this.BasicHttpSecurity != null, "this.BasicHttpSecurity should not return null from a derived class.");

            BasicHttpSecurity basicHttpSecurity = BasicHttpSecurity;
            if (basicHttpSecurity.Mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            {
                throw new PlatformNotSupportedException(nameof(BasicHttpSecurityMode.TransportWithMessageCredential));
            }
            else if (basicHttpSecurity.Mode == BasicHttpSecurityMode.Transport)
            {
                basicHttpSecurity.EnableTransportSecurity(_httpsTransport);
                return _httpsTransport;
            }
            else if (basicHttpSecurity.Mode == BasicHttpSecurityMode.TransportCredentialOnly)
            {
                basicHttpSecurity.EnableTransportAuthentication(_httpTransport);
                return _httpTransport;
            }
            else
            {
                // ensure that there is no transport security
                basicHttpSecurity.DisableTransportAuthentication(_httpTransport);
                return _httpTransport;
            }
        }

        internal virtual void CheckSettings()
        {
            //BasicHttpSecurity security = this.BasicHttpSecurity;
            //if (security == null)
            //{
            //    return;
            //}

            //BasicHttpSecurityMode mode = security.Mode;
            //if (mode == BasicHttpSecurityMode.None)
            //{
            //    return;
            //}
            //else if (mode == BasicHttpSecurityMode.Message || mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Mode", mode)));
            //}

            //// Transport.ClientCredentialType = InheritedFromHost are not supported.
            //Fx.Assert(
            //    (mode == BasicHttpSecurityMode.Transport) || (mode == BasicHttpSecurityMode.TransportCredentialOnly),
            //    "Unexpected BasicHttpSecurityMode value: " + mode);
            //HttpTransportSecurity transport = security.Transport;
            //if (transport != null && transport.ClientCredentialType == HttpClientCredentialType.InheritedFromHost)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Transport.ClientCredentialType", transport.ClientCredentialType)));
            //}
        }

    }
}