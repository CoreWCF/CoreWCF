// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public class NetMsmqBinding : MsmqBindingBase
    {
        // private BindingElements
        private BinaryMessageEncodingBindingElement _encoding;
        private NetMsmqSecurity _security;

        public NetMsmqBinding()
        {
            Initialize();
            _security = new NetMsmqSecurity();
        }

        public NetMsmqBinding(string configurationName)
        {
            Initialize();
            _security = new NetMsmqSecurity();
        }

        public NetMsmqBinding(NetMsmqSecurityMode securityMode)
        {
            Initialize();
            _security = new NetMsmqSecurity(securityMode);
        }

        private NetMsmqBinding(NetMsmqSecurity security)
        {
            Initialize();
            Fx.Assert(security != null, "Invalid (null) NetMsmqSecurity value");
            _security = security;
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return _encoding.ReaderQuotas; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                value.CopyTo(_encoding.ReaderQuotas);
            }
        }

        public NetMsmqSecurity Security
        {
            get { return _security; }
            set { _security = value; }
        }

        public EnvelopeVersion EnvelopeVersion
        {
            get { return EnvelopeVersion.Soap12; }
        }

        // [DefaultValue(TransportDefaults.MaxBufferPoolSize)]
        public long MaxBufferPoolSize
        {
            get { return _transport.MaxBufferPoolSize; }
            set { _transport.MaxBufferPoolSize = value; }
        }

        internal int MaxPoolSize
        {
            get { return (_transport as MsmqTransportBindingElement).MaxPoolSize; }
            set { (_transport as MsmqTransportBindingElement).MaxPoolSize = value; }
        }

        [DefaultValue(MsmqDefaults.UseActiveDirectory)]
        public bool UseActiveDirectory
        {
            get { return (_transport as MsmqTransportBindingElement).UseActiveDirectory; }
            set { (_transport as MsmqTransportBindingElement).UseActiveDirectory = value; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeSecurity()
        {
            if (_security.Mode != NetMsmqSecurity.DefaultMode)
            {
                return true;
            }

            if (_security.Transport.MsmqAuthenticationMode != MsmqDefaults.MsmqAuthenticationMode1 ||
                _security.Transport.MsmqEncryptionAlgorithm != MsmqDefaults.MsmqEncryptionAlgorithm1 ||
                _security.Transport.MsmqSecureHashAlgorithm != MsmqDefaults.MsmqSecureHashAlgorithm ||
                _security.Transport.MsmqProtectionLevel != MsmqDefaults.MsmqProtectionLevel)
            {
                return true;
            }

            //if (this.security.Message.AlgorithmSuite != MsmqDefaults.MessageSecurityAlgorithmSuite ||
            //this.security.Message.ClientCredentialType != MsmqDefaults.DefaultClientCredentialType)
            //{
            //    return true;
            //}
            return false;
        }

        private void Initialize()
        {
            _transport = new MsmqTransportBindingElement();
            _encoding = new BinaryMessageEncodingBindingElement();
        }

        public override BindingElementCollection CreateBindingElements()
        {
            // return collection of BindingElements
            BindingElementCollection bindingElements = new BindingElementCollection();
            // order of BindingElements is important
            // add security
            SecurityBindingElement wsSecurity = CreateMessageSecurity();
            if (wsSecurity != null)
            {
                bindingElements.Add(wsSecurity);
            }

            // add encoding (text or mtom)
            bindingElements.Add(_encoding);
            // add transport
            bindingElements.Add(GetTransport());

            return bindingElements.Clone();
        }

        private SecurityBindingElement CreateMessageSecurity()
        {
            if (_security.Mode == NetMsmqSecurityMode.Message || _security.Mode == NetMsmqSecurityMode.Both)
            {
                return _security.CreateMessageSecurity();
            }

            return null;
        }

        private MsmqBindingElementBase GetTransport()
        {
            _security.ConfigureTransportSecurity(_transport);
            return _transport;
        }
    }
}
