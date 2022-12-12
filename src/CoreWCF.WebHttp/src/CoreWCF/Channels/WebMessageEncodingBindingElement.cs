// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Xml;

namespace CoreWCF.Channels
{
    public sealed class WebMessageEncodingBindingElement : MessageEncodingBindingElement//, IWsdlExportExtension
    {
        private int _maxReadPoolSize;
        private int _maxWritePoolSize;
        private Encoding _writeEncoding;

        public WebMessageEncodingBindingElement() : this(TextEncoderDefaults.Encoding)
        {
        }

        public WebMessageEncodingBindingElement(Encoding writeEncoding)
        {
            if (writeEncoding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));
            }

            TextEncoderDefaults.ValidateEncoding(writeEncoding);
            _maxReadPoolSize = EncoderDefaults.MaxReadPoolSize;
            _maxWritePoolSize = EncoderDefaults.MaxWritePoolSize;
            ReaderQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(ReaderQuotas);
            _writeEncoding = writeEncoding;
        }

        private WebMessageEncodingBindingElement(WebMessageEncodingBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _maxReadPoolSize = elementToBeCloned._maxReadPoolSize;
            _maxWritePoolSize = elementToBeCloned._maxWritePoolSize;
            ReaderQuotas = new XmlDictionaryReaderQuotas();
            elementToBeCloned.ReaderQuotas.CopyTo(ReaderQuotas);
            _writeEncoding = elementToBeCloned._writeEncoding;
            ContentTypeMapper = elementToBeCloned.ContentTypeMapper;
            CrossDomainScriptAccessEnabled = elementToBeCloned.CrossDomainScriptAccessEnabled;
        }

        public WebContentTypeMapper ContentTypeMapper { get; set; }

        public int MaxReadPoolSize
        {
            get
            {
                return _maxReadPoolSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SRCommon.ValueMustBePositive)));
                }

                _maxReadPoolSize = value;
            }
        }

        public int MaxWritePoolSize
        {
            get
            {
                return _maxWritePoolSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SRCommon.ValueMustBePositive)));
                }

                _maxWritePoolSize = value;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return MessageVersion.None;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value != MessageVersion.None)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(value), SR.Format(SR.JsonOnlySupportsMessageVersionNone));
                }
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas { get; }

        public Encoding WriteEncoding
        {
            get
            {
                return  _writeEncoding;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                TextEncoderDefaults.ValidateEncoding(value);
                _writeEncoding = value;
            }
        }

        public bool CrossDomainScriptAccessEnabled
        {
            get;
            set;
        }

        public override BindingElement Clone()
        {
            return new WebMessageEncodingBindingElement(this);
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new WebMessageEncoderFactory(WriteEncoding, MaxReadPoolSize, MaxWritePoolSize, ReaderQuotas, ContentTypeMapper, CrossDomainScriptAccessEnabled);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(XmlDictionaryReaderQuotas))
            {
                return (T)(object)ReaderQuotas;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        //void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext context)
        //{
        //}

        //void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context)
        //{
        //    if (context == null)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
        //    }

        //    SoapHelper.SetSoapVersion(context, exporter, this.MessageVersion.Envelope);
        //}

        protected override bool CheckEncodingVersion(EnvelopeVersion version) => MessageVersion.Envelope == version;

        protected override bool IsMatch(BindingElement b) => false;
    }
}
