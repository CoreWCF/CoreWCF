// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Authentication.ExtendedProtection;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class RequestSecurityToken : BodyWriter
    {
        private string _context;
        private string _tokenType;
        private string _requestType;
        private SecurityToken _entropyToken;
        private BinaryNegotiation _negotiationData;
        private readonly XmlElement _rstXml;
        private IList<XmlElement> _requestProperties;
        private byte[] _cachedWriteBuffer;
        private int _cachedWriteBufferLength;
        private int _keySize;
        private SecurityKeyIdentifierClause _renewTarget;
        private SecurityKeyIdentifierClause _closeTarget;
        private OnGetBinaryNegotiationCallback _onGetBinaryNegotiation;
        private SecurityStandardsManager _standardsManager;
        private object _appliesTo;
        private DataContractSerializer _appliesToSerializer;
        private Type _appliesToType;

        public RequestSecurityToken()
            : this(SecurityStandardsManager.DefaultInstance)
        {
        }

        public RequestSecurityToken(MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
            : this(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer))
        {
        }

        public RequestSecurityToken(MessageSecurityVersion messageSecurityVersion,
                                    SecurityTokenSerializer securityTokenSerializer,
                                    XmlElement requestSecurityTokenXml,
                                    string context,
                                    string tokenType,
                                    string requestType,
                                    int keySize,
                                    SecurityKeyIdentifierClause renewTarget,
                                    SecurityKeyIdentifierClause closeTarget)
            : this(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer),
                   requestSecurityTokenXml,
                   context,
                   tokenType,
                   requestType,
                   keySize,
                   renewTarget,
                   closeTarget)
        {
        }

        public RequestSecurityToken(XmlElement requestSecurityTokenXml,
                                    string context,
                                    string tokenType,
                                    string requestType,
                                    int keySize,
                                    SecurityKeyIdentifierClause renewTarget,
                                    SecurityKeyIdentifierClause closeTarget)
            : this(SecurityStandardsManager.DefaultInstance,
                   requestSecurityTokenXml,
                   context,
                   tokenType,
                   requestType,
                   keySize,
                   renewTarget,
                   closeTarget)
        {
        }

        internal RequestSecurityToken(SecurityStandardsManager standardsManager,
                                      XmlElement rstXml,
                                      string context,
                                      string tokenType,
                                      string requestType,
                                      int keySize,
                                      SecurityKeyIdentifierClause renewTarget,
                                      SecurityKeyIdentifierClause closeTarget)
            : base(true)
        {
            _standardsManager = standardsManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            _rstXml = rstXml ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstXml));
            _context = context;
            _tokenType = tokenType;
            _keySize = keySize;
            _requestType = requestType;
            _renewTarget = renewTarget;
            _closeTarget = closeTarget;
            IsReceiver = true;
            IsReadOnly = true;
        }

        internal RequestSecurityToken(SecurityStandardsManager standardsManager)
            : this(standardsManager, true)
        {
            // no op
        }

        internal RequestSecurityToken(SecurityStandardsManager standardsManager, bool isBuffered)
            : base(isBuffered)
        {
            _standardsManager = standardsManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            _requestType = _standardsManager.TrustDriver.RequestTypeIssue;
            _requestProperties = null;
            IsReceiver = false;
            IsReadOnly = false;
        }

        public ChannelBinding GetChannelBinding()
        {
            if (Message == null)
            {
                return null;
            }

            ChannelBindingMessageProperty.TryGet(Message, out ChannelBindingMessageProperty channelBindingMessageProperty);
            ChannelBinding channelBinding = null;

            if (channelBindingMessageProperty != null)
            {
                channelBinding = channelBindingMessageProperty.ChannelBinding;
            }

            return channelBinding;
        }

        /// <summary>
        /// Will hold a reference to the outbound message from which we will fish the ChannelBinding out of.
        /// </summary>
        public Message Message { get; set; }

        public string Context
        {
            get
            {
                return _context;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                _context = value;
            }
        }

        public string TokenType
        {
            get
            {
                return _tokenType;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                _tokenType = value;
            }
        }

        public int KeySize
        {
            get
            {
                return _keySize;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.ValueMustBeNonNegative));
                }

                _keySize = value;
            }
        }

        public bool IsReadOnly { get; private set; }

        public delegate void OnGetBinaryNegotiationCallback(ChannelBinding channelBinding);
        public OnGetBinaryNegotiationCallback OnGetBinaryNegotiation
        {
            get
            {
                return _onGetBinaryNegotiation;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }
                _onGetBinaryNegotiation = value;
            }
        }

        public IEnumerable<XmlElement> RequestProperties
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, nameof(RequestProperties))));
                }
                return _requestProperties;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                if (value != null)
                {
                    int index = 0;
                    Collection<XmlElement> coll = new Collection<XmlElement>();
                    foreach (XmlElement property in value)
                    {
                        if (property == null)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(string.Format(CultureInfo.InvariantCulture, "value[{0}]", index)));
                        }

                        coll.Add(property);
                        ++index;
                    }
                    _requestProperties = coll;
                }
                else
                {
                    _requestProperties = null;
                }
            }
        }

        public string RequestType
        {
            get
            {
                return _requestType;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                _requestType = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public SecurityKeyIdentifierClause RenewTarget
        {
            get
            {
                return _renewTarget;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                _renewTarget = value;
            }
        }

        public SecurityKeyIdentifierClause CloseTarget
        {
            get
            {
                return _closeTarget;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                _closeTarget = value;
            }
        }

        public XmlElement RequestSecurityTokenXml
        {
            get
            {
                if (!IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTOnly, nameof(RequestSecurityTokenXml))));
                }
                return _rstXml;
            }
        }

        internal SecurityStandardsManager StandardsManager
        {
            get
            {
                return _standardsManager;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                _standardsManager = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        internal bool IsReceiver { get; }

        internal object AppliesTo
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, nameof(AppliesTo))));
                }
                return _appliesTo;
            }
        }

        internal DataContractSerializer AppliesToSerializer
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, nameof(AppliesToSerializer))));
                }
                return _appliesToSerializer;
            }
        }

        internal Type AppliesToType
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, nameof(AppliesToType))));
                }
                return _appliesToType;
            }
        }

        protected object ThisLock { get; } = new object();

        internal void SetBinaryNegotiation(BinaryNegotiation negotiation)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }

            _negotiationData = negotiation ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiation));
        }

        internal BinaryNegotiation GetBinaryNegotiation()
        {
            if (IsReceiver)
            {
                return _standardsManager.TrustDriver.GetBinaryNegotiation(this);
            }
            else if (_negotiationData == null && _onGetBinaryNegotiation != null)
            {
                _onGetBinaryNegotiation(GetChannelBinding());
            }
            return _negotiationData;
        }

        public SecurityToken GetRequestorEntropy()
        {
            return GetRequestorEntropy(null);
        }

        internal SecurityToken GetRequestorEntropy(SecurityTokenResolver resolver)
        {
            if (IsReceiver)
            {
                return _standardsManager.TrustDriver.GetEntropy(this, resolver);
            }
            else
            {
                return _entropyToken;
            }
        }

        //public void SetRequestorEntropy(byte[] entropy)
        //{
        //    if (this.IsReadOnly)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
        //    this.entropyToken = (entropy != null) ? new NonceToken(entropy) : null;
        //}

        internal void SetRequestorEntropy(WrappedKeySecurityToken entropyToken)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }

            _entropyToken = entropyToken;
        }

        public void SetAppliesTo<T>(T appliesTo, DataContractSerializer serializer)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }

            if (appliesTo != null && serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }
            _appliesTo = appliesTo;
            _appliesToSerializer = serializer;
            _appliesToType = typeof(T);
        }

        public void GetAppliesToQName(out string localName, out string namespaceUri)
        {
            if (!IsReceiver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTOnly, "MatchesAppliesTo")));
            }

            _standardsManager.TrustDriver.GetAppliesToQName(this, out localName, out namespaceUri);
        }

        public T GetAppliesTo<T>()
        {
            return GetAppliesTo<T>(DataContractSerializerDefaults.CreateSerializer(typeof(T), DataContractSerializerDefaults.MaxItemsInObjectGraph));
        }

        public T GetAppliesTo<T>(XmlObjectSerializer serializer)
        {
            if (IsReceiver)
            {
                if (serializer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
                }
                return _standardsManager.TrustDriver.GetAppliesTo<T>(this, serializer);
            }
            else
            {
                return (T)_appliesTo;
            }
        }

        private void OnWriteTo(XmlWriter writer)
        {
            if (IsReceiver)
            {
                _rstXml.WriteTo(writer);
            }
            else
            {
                _standardsManager.TrustDriver.WriteRequestSecurityToken(this, writer);
            }
        }

        public void WriteTo(XmlWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (IsReadOnly)
            {
                // cache the serialized bytes to ensure repeatability
                if (_cachedWriteBuffer == null)
                {
                    MemoryStream stream = new MemoryStream();
                    using (XmlDictionaryWriter binaryWriter = XmlDictionaryWriter.CreateBinaryWriter(stream, XD.Dictionary))
                    {
                        OnWriteTo(binaryWriter);
                        binaryWriter.Flush();
                        stream.Flush();
                        stream.Seek(0, SeekOrigin.Begin);
                        _cachedWriteBuffer = stream.GetBuffer();
                        _cachedWriteBufferLength = (int)stream.Length;
                    }
                }
                writer.WriteNode(XmlDictionaryReader.CreateBinaryReader(_cachedWriteBuffer, 0, _cachedWriteBufferLength, XD.Dictionary, XmlDictionaryReaderQuotas.Max), false);
            }
            else
            {
                OnWriteTo(writer);
            }
        }

        public static RequestSecurityToken CreateFrom(XmlReader reader)
        {
            return CreateFrom(SecurityStandardsManager.DefaultInstance, reader);
        }

        public static RequestSecurityToken CreateFrom(XmlReader reader, MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
        {
            return CreateFrom(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer), reader);
        }

        internal static RequestSecurityToken CreateFrom(SecurityStandardsManager standardsManager, XmlReader reader)
        {
            return standardsManager.TrustDriver.CreateRequestSecurityToken(reader);
        }

        public void MakeReadOnly()
        {
            if (!IsReadOnly)
            {
                IsReadOnly = true;
                if (_requestProperties != null)
                {
                    _requestProperties = new ReadOnlyCollection<XmlElement>(_requestProperties);
                }
                OnMakeReadOnly();
            }
        }

        protected internal virtual void OnWriteCustomAttributes(XmlWriter writer) { }

        protected internal virtual void OnWriteCustomElements(XmlWriter writer) { }

        protected internal virtual void OnMakeReadOnly() { }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            WriteTo(writer);
        }
    }
}
