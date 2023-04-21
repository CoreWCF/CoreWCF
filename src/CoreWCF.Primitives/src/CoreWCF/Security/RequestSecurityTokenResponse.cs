// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;
using Psha1DerivedKeyGenerator = CoreWCF.IdentityModel.Psha1DerivedKeyGenerator;

namespace CoreWCF.Security
{
    internal class RequestSecurityTokenResponse : BodyWriter
    {
        private static readonly int s_minSaneKeySizeInBits = 8 * 8; // 8 Bytes.
        private static readonly int s_maxSaneKeySizeInBits = (16 * 1024) * 8; // 16 K
        private SecurityStandardsManager _standardsManager;
        private string _context;
        private int _keySize;
        private bool _computeKey;
        private string _tokenType;
        private SecurityKeyIdentifierClause _requestedAttachedReference;
        private SecurityKeyIdentifierClause _requestedUnattachedReference;
        private SecurityToken _issuedToken;
        private SecurityToken _proofToken;
        private SecurityToken _entropyToken;
        private BinaryNegotiation _negotiationData;
        private readonly XmlElement _rstrXml;
        private bool _isLifetimeSet;
        private byte[] _authenticator;
        private byte[] _cachedWriteBuffer;
        private int _cachedWriteBufferLength;
        private bool _isRequestedTokenClosed;
        private object _appliesTo;
        private XmlObjectSerializer _appliesToSerializer;
        private Type _appliesToType;

        public RequestSecurityTokenResponse()
            : this(SecurityStandardsManager.DefaultInstance)
        {
        }

        public RequestSecurityTokenResponse(MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
            : this(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer))
        {
        }

        public RequestSecurityTokenResponse(XmlElement requestSecurityTokenResponseXml,
                                            string context,
                                            string tokenType,
                                            int keySize,
                                            SecurityKeyIdentifierClause requestedAttachedReference,
                                            SecurityKeyIdentifierClause requestedUnattachedReference,
                                            bool computeKey,
                                            DateTime validFrom,
                                            DateTime validTo,
                                            bool isRequestedTokenClosed)
            : this(SecurityStandardsManager.DefaultInstance,
                   requestSecurityTokenResponseXml,
                   context,
                   tokenType,
                   keySize,
                   requestedAttachedReference,
                   requestedUnattachedReference,
                   computeKey,
                   validFrom,
                   validTo,
                   isRequestedTokenClosed)
        {
        }

        public RequestSecurityTokenResponse(MessageSecurityVersion messageSecurityVersion,
                                            SecurityTokenSerializer securityTokenSerializer,
                                            XmlElement requestSecurityTokenResponseXml,
                                            string context,
                                            string tokenType,
                                            int keySize,
                                            SecurityKeyIdentifierClause requestedAttachedReference,
                                            SecurityKeyIdentifierClause requestedUnattachedReference,
                                            bool computeKey,
                                            DateTime validFrom,
                                            DateTime validTo,
                                            bool isRequestedTokenClosed)
            : this(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer),
                   requestSecurityTokenResponseXml,
                   context,
                   tokenType,
                   keySize,
                   requestedAttachedReference,
                   requestedUnattachedReference,
                   computeKey,
                   validFrom,
                   validTo,
                   isRequestedTokenClosed)
        {
        }

        internal RequestSecurityTokenResponse(SecurityStandardsManager standardsManager)
            : base(true)
        {
            _standardsManager = standardsManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            ValidFrom = SecurityUtils.MinUtcDateTime;
            ValidTo = SecurityUtils.MaxUtcDateTime;
            _isRequestedTokenClosed = false;
            _isLifetimeSet = false;
            IsReceiver = false;
            IsReadOnly = false;
        }

        internal RequestSecurityTokenResponse(SecurityStandardsManager standardsManager,
                                              XmlElement rstrXml,
                                              string context,
                                              string tokenType,
                                              int keySize,
                                              SecurityKeyIdentifierClause requestedAttachedReference,
                                              SecurityKeyIdentifierClause requestedUnattachedReference,
                                              bool computeKey,
                                              DateTime validFrom,
                                              DateTime validTo,
                                              bool isRequestedTokenClosed)
            : base(true)
        {
            _standardsManager = standardsManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            _rstrXml = rstrXml ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstrXml));
            _context = context;
            _tokenType = tokenType;
            _keySize = keySize;
            _requestedAttachedReference = requestedAttachedReference;
            _requestedUnattachedReference = requestedUnattachedReference;
            _computeKey = computeKey;
            ValidFrom = validFrom.ToUniversalTime();
            ValidTo = validTo.ToUniversalTime();
            _isLifetimeSet = true;
            _isRequestedTokenClosed = isRequestedTokenClosed;
            // this.issuedTokenBuffer = issuedTokenBuffer;
            IsReceiver = true;
            IsReadOnly = true;
        }

        public RequestSecurityTokenResponse(SecurityStandardsManager standardsManager,
            XmlElement rstrXml,
            string context,
            string tokenType, int keySize, SecurityKeyIdentifierClause requestedAttachedReference,
            SecurityKeyIdentifierClause requestedUnattachedReference, bool computeKey, DateTime validFrom, DateTime validTo,
            bool isRequestedTokenClosed, XmlBuffer issuedTokenBuffer) :
            this(standardsManager, rstrXml, context, tokenType, keySize, requestedAttachedReference, requestedUnattachedReference, computeKey, validFrom, validTo, isRequestedTokenClosed)
        {
            IssuedTokenBuffer = issuedTokenBuffer;
        }

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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _tokenType = value;
            }
        }

        public SecurityKeyIdentifierClause RequestedAttachedReference
        {
            get
            {
                return _requestedAttachedReference;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _requestedAttachedReference = value;
            }
        }

        public SecurityKeyIdentifierClause RequestedUnattachedReference
        {
            get
            {
                return _requestedUnattachedReference;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _requestedUnattachedReference = value;
            }
        }

        public DateTime ValidFrom { get; private set; }

        public DateTime ValidTo { get; private set; }

        public bool ComputeKey
        {
            get
            {
                return _computeKey;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _computeKey = value;
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.ValueMustBeNonNegative));
                }

                _keySize = value;
            }
        }

        public bool IsRequestedTokenClosed
        {
            get
            {
                return _isRequestedTokenClosed;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _isRequestedTokenClosed = value;
            }
        }

        public bool IsReadOnly { get; private set; }

        protected object ThisLock { get; } = new object();

        internal bool IsReceiver { get; }

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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _standardsManager = (value ?? SecurityStandardsManager.DefaultInstance);
            }
        }

        public SecurityToken EntropyToken
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRSTR, nameof(EntropyToken))));
                }
                return _entropyToken;
            }
        }

        public SecurityToken RequestedSecurityToken
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRSTR, nameof(_issuedToken))));
                }
                return _issuedToken;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _issuedToken = value;
            }
        }

        public SecurityToken RequestedProofToken
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRSTR, nameof(_proofToken))));
                }
                return _proofToken;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _proofToken = value;
            }
        }

        public XmlElement RequestSecurityTokenResponseXml
        {
            get
            {
                if (!IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTROnly, nameof(RequestSecurityTokenResponseXml))));
                }
                return _rstrXml;
            }
        }

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

        internal XmlObjectSerializer AppliesToSerializer
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

        internal bool IsLifetimeSet
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRSTR, nameof(IsLifetimeSet))));
                }
                return _isLifetimeSet;
            }
        }

        internal CoreWCF.XmlBuffer IssuedTokenBuffer { get; }

        public void SetIssuerEntropy(byte[] issuerEntropy)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            _entropyToken = (issuerEntropy != null) ? new NonceToken(issuerEntropy) : null;
        }

        internal void SetIssuerEntropy(WrappedKeySecurityToken issuerEntropy)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            _entropyToken = issuerEntropy;
        }

        public SecurityToken GetIssuerEntropy()
        {
            return GetIssuerEntropy(null);
        }

        internal SecurityToken GetIssuerEntropy(SecurityTokenResolver resolver)
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

        public void SetLifetime(DateTime validFrom, DateTime validTo)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            if (validFrom.ToUniversalTime() > validTo.ToUniversalTime())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.EffectiveGreaterThanExpiration);
            }
            ValidFrom = validFrom.ToUniversalTime();
            ValidTo = validTo.ToUniversalTime();
            _isLifetimeSet = true;
        }

        public void SetAppliesTo<T>(T appliesTo, XmlObjectSerializer serializer)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
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

        internal void SetBinaryNegotiation(BinaryNegotiation negotiation)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            _negotiationData = negotiation ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiation));
        }

        internal BinaryNegotiation GetBinaryNegotiation()
        {
            if (IsReceiver)
            {
                return _standardsManager.TrustDriver.GetBinaryNegotiation(this);
            }
            else
            {
                return _negotiationData;
            }
        }

        internal void SetAuthenticator(byte[] authenticator)
        {
            if (authenticator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authenticator));
            }

            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            _authenticator = Fx.AllocateByteArray(authenticator.Length);
            Buffer.BlockCopy(authenticator, 0, _authenticator, 0, authenticator.Length);
        }

        internal byte[] GetAuthenticator()
        {
            if (IsReceiver)
            {
                return _standardsManager.TrustDriver.GetAuthenticator(this);
            }
            else
            {
                if (_authenticator == null)
                {
                    return null;
                }
                else
                {
                    byte[] result = Fx.AllocateByteArray(_authenticator.Length);
                    Buffer.BlockCopy(_authenticator, 0, result, 0, _authenticator.Length);
                    return result;
                }
            }
        }

        private void OnWriteTo(XmlWriter w)
        {
            if (IsReceiver)
            {
                _rstrXml.WriteTo(w);
            }
            else
            {
                _standardsManager.TrustDriver.WriteRequestSecurityTokenResponse(this, w);
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

        public static RequestSecurityTokenResponse CreateFrom(XmlReader reader)
        {
            return CreateFrom(SecurityStandardsManager.DefaultInstance, reader);
        }

        public static RequestSecurityTokenResponse CreateFrom(XmlReader reader, MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
        {
            return CreateFrom(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer), reader);
        }

        internal static RequestSecurityTokenResponse CreateFrom(SecurityStandardsManager standardsManager, XmlReader reader)
        {
            return standardsManager.TrustDriver.CreateRequestSecurityTokenResponse(reader);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            WriteTo(writer);
        }

        public void MakeReadOnly()
        {
            if (!IsReadOnly)
            {
                IsReadOnly = true;
                OnMakeReadOnly();
            }
        }

        public GenericXmlSecurityToken GetIssuedToken(SecurityTokenResolver resolver, IList<SecurityTokenAuthenticator> allowedAuthenticators, SecurityKeyEntropyMode keyEntropyMode, byte[] requestorEntropy, string expectedTokenType,
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            return GetIssuedToken(resolver, allowedAuthenticators, keyEntropyMode, requestorEntropy, expectedTokenType, authorizationPolicies, 0, false);
        }

        public virtual GenericXmlSecurityToken GetIssuedToken(SecurityTokenResolver resolver, IList<SecurityTokenAuthenticator> allowedAuthenticators, SecurityKeyEntropyMode keyEntropyMode, byte[] requestorEntropy, string expectedTokenType,
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, int defaultKeySize, bool isBearerKeyType)
        {
            if (!IsReceiver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTROnly, nameof(GetIssuedToken))));
            }

            return _standardsManager.TrustDriver.GetIssuedToken(this, resolver, allowedAuthenticators, keyEntropyMode, requestorEntropy, expectedTokenType, authorizationPolicies, defaultKeySize, isBearerKeyType);
        }

        public virtual GenericXmlSecurityToken GetIssuedToken(string expectedTokenType, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, RSA clientKey)
        {
            if (!IsReceiver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTROnly, nameof(GetIssuedToken))));
            }

            return _standardsManager.TrustDriver.GetIssuedToken(this, expectedTokenType, authorizationPolicies, clientKey);
        }

        protected internal virtual void OnWriteCustomAttributes(XmlWriter writer)
        { }

        protected internal virtual void OnWriteCustomElements(XmlWriter writer)
        { }

        protected virtual void OnMakeReadOnly() { }

        public static byte[] ComputeCombinedKey(byte[] requestorEntropy, byte[] issuerEntropy, int keySizeInBits)
        {
            if (requestorEntropy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(requestorEntropy));
            }

            if (issuerEntropy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(issuerEntropy));
            }
            // Do a sanity check here. We don't want to allow invalid keys or keys that are too
            // large.
            if ((keySizeInBits < s_minSaneKeySizeInBits) || (keySizeInBits > s_maxSaneKeySizeInBits))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.InvalidKeySizeSpecifiedInNegotiation, keySizeInBits, s_minSaneKeySizeInBits, s_maxSaneKeySizeInBits)));
            }

            Psha1DerivedKeyGenerator generator = new Psha1DerivedKeyGenerator(requestorEntropy);
            return generator.GenerateDerivedKey(Array.Empty<byte>(), issuerEntropy, keySizeInBits, 0);
        }
    }
}
