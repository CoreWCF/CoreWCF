// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class BinaryMessageEncodingBindingElement : MessageEncodingBindingElement
    {
        private int _maxReadPoolSize;
        private int _maxWritePoolSize;
        private readonly XmlDictionaryReaderQuotas _readerQuotas;
        private int _maxSessionSize;
        private BinaryVersion _binaryVersion;
        private MessageVersion _messageVersion;
        private long _maxReceivedMessageSize;

        public BinaryMessageEncodingBindingElement()
        {
            _maxReadPoolSize = EncoderDefaults.MaxReadPoolSize;
            _maxWritePoolSize = EncoderDefaults.MaxWritePoolSize;
            _readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(_readerQuotas);
            _maxSessionSize = BinaryEncoderDefaults.MaxSessionSize;
            _binaryVersion = BinaryEncoderDefaults.BinaryVersion;
            _messageVersion = MessageVersion.CreateVersion(BinaryEncoderDefaults.EnvelopeVersion);
            CompressionFormat = EncoderDefaults.DefaultCompressionFormat;
        }

        private BinaryMessageEncodingBindingElement(BinaryMessageEncodingBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _maxReadPoolSize = elementToBeCloned._maxReadPoolSize;
            _maxWritePoolSize = elementToBeCloned._maxWritePoolSize;
            _readerQuotas = new XmlDictionaryReaderQuotas();
            elementToBeCloned._readerQuotas.CopyTo(_readerQuotas);
            MaxSessionSize = elementToBeCloned.MaxSessionSize;
            BinaryVersion = elementToBeCloned.BinaryVersion;
            _messageVersion = elementToBeCloned._messageVersion;
            CompressionFormat = elementToBeCloned.CompressionFormat;
            _maxReceivedMessageSize = elementToBeCloned._maxReceivedMessageSize;
        }

        [DefaultValue(EncoderDefaults.DefaultCompressionFormat)]
        public CompressionFormat CompressionFormat { get; set; }

        /* public */
        private BinaryVersion BinaryVersion
        {
            get
            {
                return _binaryVersion;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                _binaryVersion = value;
            }
        }

        public override MessageVersion MessageVersion
        {
            get { return _messageVersion; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                if (value.Envelope != BinaryEncoderDefaults.EnvelopeVersion)
                {
                    string errorMsg = SR.Format(SR.UnsupportedEnvelopeVersion, GetType().FullName, BinaryEncoderDefaults.EnvelopeVersion, value.Envelope);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(errorMsg));
                }

                _messageVersion = MessageVersion.CreateVersion(BinaryEncoderDefaults.EnvelopeVersion, value.Addressing);
            }
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
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                value.CopyTo(_readerQuotas);
            }
        }

        [DefaultValue(BinaryEncoderDefaults.MaxSessionSize)]
        public int MaxSessionSize
        {
            get
            {
                return _maxSessionSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }

                _maxSessionSize = value;
            }
        }

        private void VerifyCompression(BindingContext context)
        {
            if (CompressionFormat != CompressionFormat.None)
            {
                ITransportCompressionSupport compressionSupport = context.GetInnerProperty<ITransportCompressionSupport>();
                if (compressionSupport == null || !compressionSupport.IsCompressionFormatSupported(CompressionFormat))
                {
                    throw Fx.Exception.AsError(new NotSupportedException(SR.Format(
                        SR.TransportDoesNotSupportCompression, CompressionFormat.ToString(),
                        GetType().Name,
                        CompressionFormat.None.ToString())));
                }
            }
        }

        private void SetMaxReceivedMessageSizeFromTransport(BindingContext context)
        {
            TransportBindingElement transport = context.Binding.Elements.Find<TransportBindingElement>();
            if (transport != null)
            {
                // We are guaranteed that a transport exists when building a binding;  
                // Allow the regular flow/checks to happen rather than throw here 
                // (InternalBuildChannelListener will call into the BindingContext. Validation happens there and it will throw) 
                _maxReceivedMessageSize = transport.MaxReceivedMessageSize;
            }
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (innerDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerDispatcher));
            }

            VerifyCompression(context);
            SetMaxReceivedMessageSizeFromTransport(context);
            return context.BuildNextServiceDispatcher<TChannel>(innerDispatcher);
        }

        // TODO: Make sure this verification code is executed during pipeline build
        //public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        //{
        //    VerifyCompression(context);
        //    SetMaxReceivedMessageSizeFromTransport(context);
        //    return InternalBuildChannelListener<TChannel>(context);
        //}

        public override BindingElement Clone()
        {
            return new BinaryMessageEncodingBindingElement(this);
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new BinaryMessageEncoderFactory(
                MessageVersion,
                MaxReadPoolSize,
                MaxWritePoolSize,
                MaxSessionSize,
                ReaderQuotas,
                _maxReceivedMessageSize,
                BinaryVersion,
                CompressionFormat);
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

        protected override bool IsMatch(BindingElement b)
        {
            if (!base.IsMatch(b))
            {
                return false;
            }

            BinaryMessageEncodingBindingElement binary = b as BinaryMessageEncodingBindingElement;
            if (binary == null)
            {
                return false;
            }

            if (_maxReadPoolSize != binary.MaxReadPoolSize)
            {
                return false;
            }

            if (_maxWritePoolSize != binary.MaxWritePoolSize)
            {
                return false;
            }

            // compare XmlDictionaryReaderQuotas
            if (_readerQuotas.MaxStringContentLength != binary.ReaderQuotas.MaxStringContentLength)
            {
                return false;
            }

            if (_readerQuotas.MaxArrayLength != binary.ReaderQuotas.MaxArrayLength)
            {
                return false;
            }

            if (_readerQuotas.MaxBytesPerRead != binary.ReaderQuotas.MaxBytesPerRead)
            {
                return false;
            }

            if (_readerQuotas.MaxDepth != binary.ReaderQuotas.MaxDepth)
            {
                return false;
            }

            if (_readerQuotas.MaxNameTableCharCount != binary.ReaderQuotas.MaxNameTableCharCount)
            {
                return false;
            }

            if (MaxSessionSize != binary.MaxSessionSize)
            {
                return false;
            }

            if (CompressionFormat != binary.CompressionFormat)
            {
                return false;
            }

            return true;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeReaderQuotas()
        {
            return (!EncoderDefaults.IsDefaultReaderQuotas(ReaderQuotas));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeMessageVersion()
        {
            return (!_messageVersion.IsMatch(MessageVersion.Default));
        }
    }
}