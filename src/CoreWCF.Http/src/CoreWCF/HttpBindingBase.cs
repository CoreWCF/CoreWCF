// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public abstract class HttpBindingBase : Binding
    {
        private readonly HttpTransportBindingElement _httpTransport;
        private readonly HttpsTransportBindingElement _httpsTransport;
        private readonly TextMessageEncodingBindingElement _textEncoding;

        internal HttpBindingBase()
        {
            _httpTransport = new HttpTransportBindingElement();
            _httpsTransport = new HttpsTransportBindingElement();
            _textEncoding = new TextMessageEncodingBindingElement();
            _textEncoding.MessageVersion = MessageVersion.Soap11;
        }
        // [System.ComponentModel.DefaultValueAttribute(false)]
        // public bool AllowCookies { get { return default(bool); } set { } }

        // [System.ComponentModel.DefaultValueAttribute((long)524288)]
        // public long MaxBufferPoolSize { get { return default(long); } set { } }
        // [System.ComponentModel.DefaultValueAttribute(65536)]
        // public int MaxBufferSize { get { return default(int); } set { } }

        [DefaultValue(TransportDefaults.MaxReceivedMessageSize)]
        public long MaxReceivedMessageSize
        {
            get
            {
                return _httpTransport.MaxReceivedMessageSize;
            }
            set
            {
                _httpTransport.MaxReceivedMessageSize = value;
                //_httpsTransport.MaxReceivedMessageSize = value;
            }
        }

        public int MaxBufferSize
        {
            get { return _httpTransport.MaxBufferSize; }
            set
            {
                _httpTransport.MaxBufferSize = value;
                //_httpsTransport.MaxBufferSize = value;
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return _textEncoding.ReaderQuotas;
            }

            set
            {
                if (value == null)
                {
                    throw Fx.Exception.ArgumentNull(nameof(value));
                }

                value.CopyTo(_textEncoding.ReaderQuotas);
                //value.CopyTo(this.mtomEncoding.ReaderQuotas);

                SetReaderQuotas(value);
            }
        }

        public override string Scheme { get { return GetTransport().Scheme; } }

        public Encoding TextEncoding
        {
            get
            {
                return _textEncoding.WriteEncoding;
            }

            set
            {
                _textEncoding.WriteEncoding = value;
                //_mtomEncoding.WriteEncoding = value;
            }
        }

        [DefaultValue(HttpTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get
            {
                return _httpTransport.TransferMode;
            }

            set
            {
                _httpTransport.TransferMode = value;
                _httpsTransport.TransferMode = value;
            }
        }

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
            Fx.Assert(BasicHttpSecurity != null, "this.BasicHttpSecurity should not return null from a derived class.");

            BasicHttpSecurity basicHttpSecurity = BasicHttpSecurity;
            if (basicHttpSecurity.Mode == BasicHttpSecurityMode.TransportWithMessageCredential || basicHttpSecurity.Mode == BasicHttpSecurityMode.Message)
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

        internal virtual void SetReaderQuotas(XmlDictionaryReaderQuotas readerQuotas)
        {
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