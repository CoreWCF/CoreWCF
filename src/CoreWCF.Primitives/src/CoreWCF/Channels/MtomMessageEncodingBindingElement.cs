// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Text;
using System.Xml;

namespace CoreWCF.Channels
{
    public sealed class MtomMessageEncodingBindingElement : MessageEncodingBindingElement
    {
        int _maxReadPoolSize;
        int _maxWritePoolSize;
        XmlDictionaryReaderQuotas _readerQuotas;
        int _maxBufferSize;
        Encoding _writeEncoding;
        MessageVersion _messageVersion;

        public MtomMessageEncodingBindingElement() : this(MessageVersion.Default, TextEncoderDefaults.Encoding) { }

        public MtomMessageEncodingBindingElement(MessageVersion messageVersion, Encoding writeEncoding)
        {
            if (messageVersion == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));

            if (messageVersion == MessageVersion.None)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MtomEncoderBadMessageVersion, messageVersion.ToString()), nameof(messageVersion)));

            if (writeEncoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));

            TextEncoderDefaults.ValidateEncoding(writeEncoding);
            _maxReadPoolSize = EncoderDefaults.MaxReadPoolSize;
            _maxWritePoolSize = EncoderDefaults.MaxWritePoolSize;
            _readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(_readerQuotas);
            _maxBufferSize = MtomEncoderDefaults.MaxBufferSize;
            _messageVersion = messageVersion;
            _writeEncoding = writeEncoding;
        }

        MtomMessageEncodingBindingElement(MtomMessageEncodingBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _maxReadPoolSize = elementToBeCloned._maxReadPoolSize;
            _maxWritePoolSize = elementToBeCloned._maxWritePoolSize;
            _readerQuotas = new XmlDictionaryReaderQuotas();
            elementToBeCloned._readerQuotas.CopyTo(_readerQuotas);
            _maxBufferSize = elementToBeCloned._maxBufferSize;
            _writeEncoding = elementToBeCloned._writeEncoding;
            _messageVersion = elementToBeCloned._messageVersion;
        }

        [DefaultValue(EncoderDefaults.MaxReadPoolSize)]
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
                                                    SR.ValueMustBePositive));
                }
                _maxReadPoolSize = value;
            }
        }

        [DefaultValue(EncoderDefaults.MaxWritePoolSize)]
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
                                                    SR.ValueMustBePositive));
                }
                _maxWritePoolSize = value;
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return _readerQuotas;
            }
        }

        [DefaultValue(MtomEncoderDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get
            {
                return _maxBufferSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBePositive));
                }
                _maxBufferSize = value;
            }
        }

        public Encoding WriteEncoding
        {
            get
            {
                return _writeEncoding;
            }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

                TextEncoderDefaults.ValidateEncoding(value);
                _writeEncoding = value;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return _messageVersion;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                if (value == MessageVersion.None)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MtomEncoderBadMessageVersion, value.ToString()), nameof(value)));
                }

                _messageVersion = value;
            }
        }

        // public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        // {
        //     return InternalBuildChannelFactory<TChannel>(context);
        // }
        //
        // public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        // {
        //     return InternalCanBuildChannelFactory<TChannel>(context);
        // }

        public override BindingElement Clone()
        {
            return new MtomMessageEncodingBindingElement(this);
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new MtomMessageEncoderFactory(MessageVersion, WriteEncoding, MaxReadPoolSize, MaxWritePoolSize, MaxBufferSize, ReaderQuotas);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(XmlDictionaryReaderQuotas))
            {
                return (T)(object)_readerQuotas;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        internal override bool CheckEncodingVersion(EnvelopeVersion version)
        {
            return _messageVersion.Envelope == version;
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (!base.IsMatch(b))
                return false;
            MtomMessageEncodingBindingElement mtom = b as MtomMessageEncodingBindingElement;
            if (mtom == null)
                return false;
            if (_maxReadPoolSize != mtom.MaxReadPoolSize)
                return false;
            if (_maxWritePoolSize != mtom.MaxWritePoolSize)
                return false;

            // compare XmlDictionaryReaderQuotas
            if (_readerQuotas.MaxStringContentLength != mtom.ReaderQuotas.MaxStringContentLength)
                return false;
            if (_readerQuotas.MaxArrayLength != mtom.ReaderQuotas.MaxArrayLength)
                return false;
            if (_readerQuotas.MaxBytesPerRead != mtom.ReaderQuotas.MaxBytesPerRead)
                return false;
            if (_readerQuotas.MaxDepth != mtom.ReaderQuotas.MaxDepth)
                return false;
            if (_readerQuotas.MaxNameTableCharCount != mtom.ReaderQuotas.MaxNameTableCharCount)
                return false;

            if (_maxBufferSize != mtom.MaxBufferSize)
                return false;

            if (WriteEncoding.EncodingName != mtom.WriteEncoding.EncodingName)
                return false;
            if (!MessageVersion.IsMatch(mtom.MessageVersion))
                return false;

            return true;
        }
    }
}
