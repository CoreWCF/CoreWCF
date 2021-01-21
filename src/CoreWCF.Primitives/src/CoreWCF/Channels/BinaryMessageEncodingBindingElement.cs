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
        int maxReadPoolSize;
        int maxWritePoolSize;
        XmlDictionaryReaderQuotas readerQuotas;
        int maxSessionSize;
        BinaryVersion binaryVersion;
        MessageVersion messageVersion;
        CompressionFormat compressionFormat;
        long maxReceivedMessageSize;

        public BinaryMessageEncodingBindingElement()
        {
            maxReadPoolSize = EncoderDefaults.MaxReadPoolSize;
            maxWritePoolSize = EncoderDefaults.MaxWritePoolSize;
            readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(readerQuotas);
            maxSessionSize = BinaryEncoderDefaults.MaxSessionSize;
            binaryVersion = BinaryEncoderDefaults.BinaryVersion;
            messageVersion = MessageVersion.CreateVersion(BinaryEncoderDefaults.EnvelopeVersion);
            compressionFormat = EncoderDefaults.DefaultCompressionFormat;
        }

        BinaryMessageEncodingBindingElement(BinaryMessageEncodingBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            maxReadPoolSize = elementToBeCloned.maxReadPoolSize;
            maxWritePoolSize = elementToBeCloned.maxWritePoolSize;
            readerQuotas = new XmlDictionaryReaderQuotas();
            elementToBeCloned.readerQuotas.CopyTo(readerQuotas);
            MaxSessionSize = elementToBeCloned.MaxSessionSize;
            BinaryVersion = elementToBeCloned.BinaryVersion;
            messageVersion = elementToBeCloned.messageVersion;
            CompressionFormat = elementToBeCloned.CompressionFormat;
            maxReceivedMessageSize = elementToBeCloned.maxReceivedMessageSize;
        }

        [DefaultValue(EncoderDefaults.DefaultCompressionFormat)]
        public CompressionFormat CompressionFormat
        {
            get
            {
                return compressionFormat;
            }
            set
            {
                compressionFormat = value;
            }
        }

        /* public */
        BinaryVersion BinaryVersion
        {
            get
            {
                return binaryVersion;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                binaryVersion = value;
            }
        }

        public override MessageVersion MessageVersion
        {
            get { return messageVersion; }
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

                messageVersion = MessageVersion.CreateVersion(BinaryEncoderDefaults.EnvelopeVersion, value.Addressing);
            }
        }

        [DefaultValue(EncoderDefaults.MaxReadPoolSize)]
        public int MaxReadPoolSize
        {
            get
            {
                return maxReadPoolSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBePositive));
                }
                maxReadPoolSize = value;
            }
        }

        [DefaultValue(EncoderDefaults.MaxWritePoolSize)]
        public int MaxWritePoolSize
        {
            get
            {
                return maxWritePoolSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBePositive));
                }
                maxWritePoolSize = value;
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return readerQuotas;
            }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                value.CopyTo(readerQuotas);
            }
        }

        [DefaultValue(BinaryEncoderDefaults.MaxSessionSize)]
        public int MaxSessionSize
        {
            get
            {
                return maxSessionSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }

                maxSessionSize = value;
            }
        }

        private void VerifyCompression(BindingContext context)
        {
            if (compressionFormat != CompressionFormat.None)
            {
                ITransportCompressionSupport compressionSupport = context.GetInnerProperty<ITransportCompressionSupport>();
                if (compressionSupport == null || !compressionSupport.IsCompressionFormatSupported(this.compressionFormat))
                {
                    throw Fx.Exception.AsError(new NotSupportedException(SR.Format(
                        SR.TransportDoesNotSupportCompression, compressionFormat.ToString(),
                        GetType().Name,
                        CompressionFormat.None.ToString())));
                }
            }
        }

        void SetMaxReceivedMessageSizeFromTransport(BindingContext context)
        {
            TransportBindingElement transport = context.Binding.Elements.Find<TransportBindingElement>();
            if (transport != null)
            {
                // We are guaranteed that a transport exists when building a binding;  
                // Allow the regular flow/checks to happen rather than throw here 
                // (InternalBuildChannelListener will call into the BindingContext. Validation happens there and it will throw) 
                maxReceivedMessageSize = transport.MaxReceivedMessageSize;
            }
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            if (innerDispatcher == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerDispatcher));

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
                maxReceivedMessageSize,
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
                return (T)(object)readerQuotas;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (!base.IsMatch(b))
                return false;

            BinaryMessageEncodingBindingElement binary = b as BinaryMessageEncodingBindingElement;
            if (binary == null)
                return false;
            if (maxReadPoolSize != binary.MaxReadPoolSize)
                return false;
            if (maxWritePoolSize != binary.MaxWritePoolSize)
                return false;

            // compare XmlDictionaryReaderQuotas
            if (readerQuotas.MaxStringContentLength != binary.ReaderQuotas.MaxStringContentLength)
                return false;
            if (readerQuotas.MaxArrayLength != binary.ReaderQuotas.MaxArrayLength)
                return false;
            if (readerQuotas.MaxBytesPerRead != binary.ReaderQuotas.MaxBytesPerRead)
                return false;
            if (readerQuotas.MaxDepth != binary.ReaderQuotas.MaxDepth)
                return false;
            if (readerQuotas.MaxNameTableCharCount != binary.ReaderQuotas.MaxNameTableCharCount)
                return false;

            if (MaxSessionSize != binary.MaxSessionSize)
                return false;
            if (CompressionFormat != binary.CompressionFormat)
                return false;
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
            return (!messageVersion.IsMatch(MessageVersion.Default));
        }
    }

}