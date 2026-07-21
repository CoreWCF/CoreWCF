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
        private MtomMessageEncodingBindingElement _mtomEncoding;
        private OptionalReliableSession _reliableSession;

        protected WSHttpBindingBase()
            : base()
        {
            Initialize();
        }

        protected WSHttpBindingBase(bool reliableSessionEnabled) : this()
        {
            ReliableSession.Enabled = reliableSessionEnabled;
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
                _mtomEncoding.MaxBufferSize = (int)value;
            }
        }

        public WSMessageEncoding MessageEncoding { get; set; }

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
                value.CopyTo(_mtomEncoding.ReaderQuotas);
            }
        }

        public OptionalReliableSession ReliableSession
        {
            get { return _reliableSession; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }

                _reliableSession.Ordered = value.Ordered;
                _reliableSession.InactivityTimeout = value.InactivityTimeout;
                _reliableSession.Enabled = value.Enabled;
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
        //        _mtomEncoding.WriteEncoding = value;
        //    }
        //}

        internal HttpTransportBindingElement HttpTransport { get; private set; }

        internal HttpsTransportBindingElement HttpsTransport { get; private set; }

        internal ReliableSessionBindingElement ReliableSessionBindingElement { get; private set; }

        private void Initialize()
        {
            HttpTransport = new HttpTransportBindingElement();
            HttpsTransport = new HttpsTransportBindingElement();
            ReliableSessionBindingElement = new ReliableSessionBindingElement(true);
            _textEncoding = new TextMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.Soap12WSAddressing10
            };
            _mtomEncoding = new MtomMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.Soap12WSAddressing10
            };
            _reliableSession = new CoreWCF.OptionalReliableSession(ReliableSessionBindingElement);
        }

        public override BindingElementCollection CreateBindingElements()
        {   // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // context

            // reliable
            if (_reliableSession.Enabled)
            {
                bindingElements.Add(ReliableSessionBindingElement);
            }

            // add security (*optional)
            SecurityBindingElement wsSecurity = CreateMessageSecurity();
            if (wsSecurity != null)
            {
                bindingElements.Add(wsSecurity);
            }

            // add encoding (text or mtom)
            WSMessageEncodingHelper.SyncUpEncodingBindingElementProperties(_textEncoding, _mtomEncoding);
            if (MessageEncoding == WSMessageEncoding.Text)
            {
                bindingElements.Add(_textEncoding);
            }
            else if (MessageEncoding == WSMessageEncoding.Mtom)
            {
                bindingElements.Add(_mtomEncoding);
            }

            // add transport (http or https)
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }

        protected abstract TransportBindingElement GetTransport();
        protected abstract SecurityBindingElement CreateMessageSecurity();
    }
}
