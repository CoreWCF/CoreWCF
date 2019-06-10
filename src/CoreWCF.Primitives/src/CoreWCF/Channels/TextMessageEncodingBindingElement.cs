﻿using System;
using System.Text;
using System.Xml;

namespace CoreWCF.Channels
{
    public sealed class TextMessageEncodingBindingElement : MessageEncodingBindingElement
    {
        int _maxReadPoolSize;
        int _maxWritePoolSize;
        XmlDictionaryReaderQuotas _readerQuotas;
        MessageVersion _messageVersion;
        Encoding _writeEncoding;

        public TextMessageEncodingBindingElement()
            : this(MessageVersion.Default, TextEncoderDefaults.Encoding)
        {
        }

        public TextMessageEncodingBindingElement(MessageVersion messageVersion, Encoding writeEncoding)
        {
            if (messageVersion == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));

            if (writeEncoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));

            TextEncoderDefaults.ValidateEncoding(writeEncoding);

            _maxReadPoolSize = EncoderDefaults.MaxReadPoolSize;
            _maxWritePoolSize = EncoderDefaults.MaxWritePoolSize;
            _readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(_readerQuotas);
            _messageVersion = messageVersion;
            _writeEncoding = writeEncoding;
        }

        TextMessageEncodingBindingElement(TextMessageEncodingBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _maxReadPoolSize = elementToBeCloned._maxReadPoolSize;
            _maxWritePoolSize = elementToBeCloned._maxWritePoolSize;
            _readerQuotas = new XmlDictionaryReaderQuotas();
            elementToBeCloned._readerQuotas.CopyTo(_readerQuotas);
            _writeEncoding = elementToBeCloned._writeEncoding;
            _messageVersion = elementToBeCloned._messageVersion;
        }

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
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                value.CopyTo(_readerQuotas);
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

                _messageVersion = value;
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
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                TextEncoderDefaults.ValidateEncoding(value);
                _writeEncoding = value;
            }
        }

        public override BindingElement Clone()
        {
            return new TextMessageEncodingBindingElement(this);
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new TextMessageEncoderFactory(MessageVersion, WriteEncoding, MaxReadPoolSize, MaxWritePoolSize, ReaderQuotas);
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

            TextMessageEncodingBindingElement text = b as TextMessageEncodingBindingElement;
            if (text == null)
                return false;
            if (_maxReadPoolSize != text.MaxReadPoolSize)
                return false;
            if (_maxWritePoolSize != text.MaxWritePoolSize)
                return false;

            // compare XmlDictionaryReaderQuotas
            if (_readerQuotas.MaxStringContentLength != text.ReaderQuotas.MaxStringContentLength)
                return false;
            if (_readerQuotas.MaxArrayLength != text.ReaderQuotas.MaxArrayLength)
                return false;
            if (_readerQuotas.MaxBytesPerRead != text.ReaderQuotas.MaxBytesPerRead)
                return false;
            if (_readerQuotas.MaxDepth != text.ReaderQuotas.MaxDepth)
                return false;
            if (_readerQuotas.MaxNameTableCharCount != text.ReaderQuotas.MaxNameTableCharCount)
                return false;

            if (WriteEncoding.EncodingName != text.WriteEncoding.EncodingName)
                return false;
            if (!MessageVersion.IsMatch(text.MessageVersion))
                return false;

            return true;
        }
    }
}