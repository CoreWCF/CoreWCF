// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Configuration;
using System.Text;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class WebHttpBindingElement : StandardBindingElement
    {
        private static readonly Type WebContentTypeMapperType = typeof(WebContentTypeMapper);

        public WebHttpBindingElement(string name)
            : base(name)
        {
        }

        public WebHttpBindingElement()
            : this(null)
        {
        }

        [ConfigurationProperty(ConfigurationStrings.AllowCookies, DefaultValue = HttpTransportDefaults.AllowCookies)]
        public bool AllowCookies
        {
            get { return (bool)base[ConfigurationStrings.AllowCookies]; }
            set { base[ConfigurationStrings.AllowCookies] = value; }

        }

        [ConfigurationProperty(ConfigurationStrings.BypassProxyOnLocal, DefaultValue = HttpTransportDefaults.BypassProxyOnLocal)]
        public bool BypassProxyOnLocal
        {
            get { return (bool)base[ConfigurationStrings.BypassProxyOnLocal]; }
            set { base[ConfigurationStrings.BypassProxyOnLocal] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.HostNameComparisonMode, DefaultValue = HttpTransportDefaults.HostNameComparisonMode)]
        public HostNameComparisonMode HostNameComparisonMode
        {
            get { return (HostNameComparisonMode)base[ConfigurationStrings.HostNameComparisonMode]; }
            set { base[ConfigurationStrings.HostNameComparisonMode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBufferPoolSize, DefaultValue = TransportDefaults.MaxBufferPoolSize)]
        [LongValidator(MinValue = 0)]
        public long MaxBufferPoolSize
        {
            get { return (long)base[ConfigurationStrings.MaxBufferPoolSize]; }
            set { base[ConfigurationStrings.MaxBufferPoolSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBufferSize, DefaultValue = TransportDefaults.MaxBufferSize)]
        [IntegerValidator(MinValue = 1)]
        public int MaxBufferSize
        {
            get { return (int)base[ConfigurationStrings.MaxBufferSize]; }
            set { base[ConfigurationStrings.MaxBufferSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxReceivedMessageSize, DefaultValue = TransportDefaults.MaxReceivedMessageSize)]
        [LongValidator(MinValue = 1)]
        public long MaxReceivedMessageSize
        {
            get { return (long)base[ConfigurationStrings.MaxReceivedMessageSize]; }
            set { base[ConfigurationStrings.MaxReceivedMessageSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ProxyAddress, DefaultValue = HttpTransportDefaults.ProxyAddress)]
        public Uri ProxyAddress
        {
            get { return (Uri)base[ConfigurationStrings.ProxyAddress]; }
            set { base[ConfigurationStrings.ProxyAddress] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReaderQuotas)]
        public XmlDictionaryReaderQuotasElement ReaderQuotas
        {
            get { return (XmlDictionaryReaderQuotasElement)base[ConfigurationStrings.ReaderQuotas]; }
        }

        [ConfigurationProperty(ConfigurationStrings.Security)]
        public WebHttpSecurityElement Security
        {
            get { return (WebHttpSecurityElement)base[ConfigurationStrings.Security]; }
        }

        [ConfigurationProperty(ConfigurationStrings.TransferMode, DefaultValue = HttpTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get { return (TransferMode)base[ConfigurationStrings.TransferMode]; }
            set { base[ConfigurationStrings.TransferMode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.UseDefaultWebProxy, DefaultValue = HttpTransportDefaults.UseDefaultWebProxy)]
        public bool UseDefaultWebProxy
        {
            get { return (bool)base[ConfigurationStrings.UseDefaultWebProxy]; }
            set { base[ConfigurationStrings.UseDefaultWebProxy] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.WriteEncoding, DefaultValue = TextEncoderDefaults.EncodingString)]
        [TypeConverter(typeof(EncodingConverter))]
        public Encoding WriteEncoding
        {
            get { return (Encoding)base[ConfigurationStrings.WriteEncoding]; }
            set { base[ConfigurationStrings.WriteEncoding] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ContentTypeMapper, DefaultValue = "")]
        [StringValidator(MinLength = 0)]
        public string ContentTypeMapper
        {
            get { return (string)base[ConfigurationStrings.ContentTypeMapper]; }
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    value = String.Empty;
                }
                base[ConfigurationStrings.ContentTypeMapper] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.CrossDomainScriptAccessEnabled, DefaultValue = false)]
        public bool CrossDomainScriptAccessEnabled
        {
            get { return (bool)base[ConfigurationStrings.CrossDomainScriptAccessEnabled]; }
            set { base[ConfigurationStrings.CrossDomainScriptAccessEnabled] = value; }
        }

        public override Binding CreateBinding()
        {
            var binding = new WebHttpBinding(WebHttpSecurityMode.None)
            {
                Name = Name,
                MaxBufferPoolSize = MaxBufferPoolSize,
                MaxBufferSize = MaxBufferSize,
                MaxReceivedMessageSize = MaxReceivedMessageSize,
                TransferMode = TransferMode,
                // commented due to null reference exception
                //CrossDomainScriptAccessEnabled = CrossDomainScriptAccessEnabled,
                ContentTypeMapper = GetContentTypeMapper(this.ContentTypeMapper),
                ReaderQuotas = ReaderQuotas.Clone(),
                ReceiveTimeout = ReceiveTimeout,
                OpenTimeout = OpenTimeout,
                CloseTimeout = CloseTimeout,
                SendTimeout = SendTimeout,
                WriteEncoding = WriteEncoding,                
            };

            Security.ApplyConfiguration(binding.Security);
            return binding;
        }

        internal static WebContentTypeMapper GetContentTypeMapper(string contentTypeMapperType)
        {
            WebContentTypeMapper contentTypeMapper = null;
            if (!string.IsNullOrEmpty(contentTypeMapperType))
            {
                Type type = System.Type.GetType(contentTypeMapperType, true);
                if (!WebContentTypeMapperType.IsAssignableFrom(type))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.ConfigInvalidType));
                }
                contentTypeMapper = (WebContentTypeMapper)Activator.CreateInstance(type);
            }
            return contentTypeMapper;
        }
    }
}
