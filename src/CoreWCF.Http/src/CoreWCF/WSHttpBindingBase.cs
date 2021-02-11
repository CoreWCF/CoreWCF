// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    public abstract class WSHttpBindingBase : Binding //, IBindingRuntimePreferences
    {
        private TextMessageEncodingBindingElement _textEncoding;

        protected WSHttpBindingBase()
            : base()
        {
            Initialize();
        }

        protected WSHttpBindingBase(bool reliableSessionEnabled) : this()
        {
            if (reliableSessionEnabled)
            {
                throw new PlatformNotSupportedException();
            }
        }

        [DefaultValue(false)]
        public bool TransactionFlow
        {
            get { return false; }
            set
            {
                if (value)
                {
                    throw new PlatformNotSupportedException();
                }
            }
        }

        // [DefaultValue(TransportDefaults.MaxBufferPoolSize)]
        public long MaxBufferPoolSize
        {
            get { return HttpTransport.MaxBufferPoolSize; }
            set
            {
                HttpTransport.MaxBufferPoolSize = value;
                HttpsTransport.MaxBufferPoolSize = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxReceivedMessageSize)]
        public long MaxReceivedMessageSize
        {
            get { return HttpTransport.MaxReceivedMessageSize; }
            set
            {
                if (value > int.MaxValue)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ArgumentOutOfRangeException(nameof(MaxReceivedMessageSize),
                        SR.MaxReceivedMessageSizeMustBeInIntegerRange));
                }
                HttpTransport.MaxReceivedMessageSize = value;
                HttpsTransport.MaxReceivedMessageSize = value;
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return _textEncoding.ReaderQuotas; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                value.CopyTo(_textEncoding.ReaderQuotas);
            }
        }

        public override string Scheme { get { return GetTransport().Scheme; } }

        public EnvelopeVersion EnvelopeVersion
        {
            get { return EnvelopeVersion.Soap12; }
        }

        //public Text.Encoding TextEncoding
        //{
        //    get { return _textEncoding.WriteEncoding; }
        //    set
        //    {
        //        _textEncoding.WriteEncoding = value;
        //    }
        //}

        internal HttpTransportBindingElement HttpTransport { get; private set; }

        internal HttpsTransportBindingElement HttpsTransport { get; private set; }

        private void Initialize()
        {
            HttpTransport = new HttpTransportBindingElement();
            HttpsTransport = new HttpsTransportBindingElement();
            _textEncoding = new TextMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.Soap12WSAddressing10
            };
        }

        public override BindingElementCollection CreateBindingElements()
        {   // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // context

            // add security (*optional)
            SecurityBindingElement wsSecurity = CreateMessageSecurity();
            if (wsSecurity != null)
            {
                bindingElements.Add(wsSecurity);
            }

            // add encoding
            bindingElements.Add(_textEncoding);

            // add transport (http or https)
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }

        protected abstract TransportBindingElement GetTransport();
        protected abstract SecurityBindingElement CreateMessageSecurity();
    }
}
