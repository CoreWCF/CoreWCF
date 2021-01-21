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
        private static readonly int minSaneKeySizeInBits = 8 * 8; // 8 Bytes.
        private static readonly int maxSaneKeySizeInBits = (16 * 1024) * 8; // 16 K
        private SecurityStandardsManager standardsManager;
        private string context;
        private int keySize;
        private bool computeKey;
        private string tokenType;
        private SecurityKeyIdentifierClause requestedAttachedReference;
        private SecurityKeyIdentifierClause requestedUnattachedReference;
        private SecurityToken issuedToken;
        private SecurityToken proofToken;
        private SecurityToken entropyToken;
        private BinaryNegotiation negotiationData;
        private readonly XmlElement rstrXml;
        private DateTime expirationTime;
        private bool isLifetimeSet;
        private byte[] authenticator;
        private bool isReadOnly;
        private byte[] cachedWriteBuffer;
        private int cachedWriteBufferLength;
        private bool isRequestedTokenClosed;
        private object appliesTo;
        private XmlObjectSerializer appliesToSerializer;
        private Type appliesToType;
        private readonly XmlBuffer issuedTokenBuffer;

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
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            }
            this.standardsManager = standardsManager;
            ValidFrom = SecurityUtils.MinUtcDateTime;
            expirationTime = SecurityUtils.MaxUtcDateTime;
            isRequestedTokenClosed = false;
            isLifetimeSet = false;
            IsReceiver = false;
            isReadOnly = false;
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
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            }
            this.standardsManager = standardsManager;
            if (rstrXml == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstrXml));
            }

            this.rstrXml = rstrXml;
            this.context = context;
            this.tokenType = tokenType;
            this.keySize = keySize;
            this.requestedAttachedReference = requestedAttachedReference;
            this.requestedUnattachedReference = requestedUnattachedReference;
            this.computeKey = computeKey;
            ValidFrom = validFrom.ToUniversalTime();
            expirationTime = validTo.ToUniversalTime();
            isLifetimeSet = true;
            this.isRequestedTokenClosed = isRequestedTokenClosed;
            // this.issuedTokenBuffer = issuedTokenBuffer;
            IsReceiver = true;
            isReadOnly = true;
        }

        public RequestSecurityTokenResponse(SecurityStandardsManager standardsManager,
            XmlElement rstrXml,
            string context,
            string tokenType, int keySize, SecurityKeyIdentifierClause requestedAttachedReference,
            SecurityKeyIdentifierClause requestedUnattachedReference, bool computeKey, DateTime validFrom, DateTime validTo,
            bool isRequestedTokenClosed, XmlBuffer issuedTokenBuffer) :
            this(standardsManager, rstrXml, context, tokenType, keySize, requestedAttachedReference, requestedUnattachedReference, computeKey, validFrom, validTo, isRequestedTokenClosed)
        {
            this.issuedTokenBuffer = issuedTokenBuffer;
        }

        public string Context
        {
            get
            {
                return context;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                context = value;
            }
        }

        public string TokenType
        {
            get
            {
                return tokenType;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                tokenType = value;
            }
        }

        public SecurityKeyIdentifierClause RequestedAttachedReference
        {
            get
            {
                return requestedAttachedReference;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                requestedAttachedReference = value;
            }
        }

        public SecurityKeyIdentifierClause RequestedUnattachedReference
        {
            get
            {
                return requestedUnattachedReference;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                requestedUnattachedReference = value;
            }
        }

        public DateTime ValidFrom { get; private set; }

        public DateTime ValidTo => expirationTime;

        public bool ComputeKey
        {
            get
            {
                return computeKey;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                computeKey = value;
            }
        }

        public int KeySize
        {
            get
            {
                return keySize;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeNonNegative));
                }

                keySize = value;
            }
        }

        public bool IsRequestedTokenClosed
        {
            get
            {
                return isRequestedTokenClosed;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                isRequestedTokenClosed = value;
            }
        }

        public bool IsReadOnly => isReadOnly;

        protected Object ThisLock { get; } = new Object();

        internal bool IsReceiver { get; }

        internal SecurityStandardsManager StandardsManager
        {
            get
            {
                return standardsManager;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                standardsManager = (value != null ? value : SecurityStandardsManager.DefaultInstance);
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
                return entropyToken;
            }
        }

        public SecurityToken RequestedSecurityToken
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRSTR, nameof(issuedToken))));
                }
                return issuedToken;
            }
            set
            {
                if (isReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                issuedToken = value;
            }
        }

        public SecurityToken RequestedProofToken
        {
            get
            {
                if (IsReceiver)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRSTR, nameof(proofToken))));
                }
                return proofToken;
            }
            set
            {
                if (isReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                proofToken = value;
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
                return rstrXml;
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
                return appliesTo;
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
                return appliesToSerializer;
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
                return appliesToType;
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
                return isLifetimeSet;
            }
        }

        internal CoreWCF.XmlBuffer IssuedTokenBuffer => issuedTokenBuffer;

        public void SetIssuerEntropy(byte[] issuerEntropy)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            entropyToken = (issuerEntropy != null) ? new NonceToken(issuerEntropy) : null;
        }

        internal void SetIssuerEntropy(WrappedKeySecurityToken issuerEntropy)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            entropyToken = issuerEntropy;
        }

        public SecurityToken GetIssuerEntropy()
        {
            return GetIssuerEntropy(null);
        }

        internal SecurityToken GetIssuerEntropy(SecurityTokenResolver resolver)
        {
            if (IsReceiver)
            {
                return standardsManager.TrustDriver.GetEntropy(this, resolver);
            }
            else
            {
                return entropyToken;
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
            expirationTime = validTo.ToUniversalTime();
            isLifetimeSet = true;
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
            this.appliesTo = appliesTo;
            appliesToSerializer = serializer;
            appliesToType = typeof(T);
        }

        public void GetAppliesToQName(out string localName, out string namespaceUri)
        {
            if (!IsReceiver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTOnly, "MatchesAppliesTo")));
            }

            standardsManager.TrustDriver.GetAppliesToQName(this, out localName, out namespaceUri);
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
                return standardsManager.TrustDriver.GetAppliesTo<T>(this, serializer);
            }
            else
            {
                return (T)appliesTo;
            }
        }

        internal void SetBinaryNegotiation(BinaryNegotiation negotiation)
        {
            if (negotiation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiation));
            }

            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            negotiationData = negotiation;
        }

        internal BinaryNegotiation GetBinaryNegotiation()
        {
            if (IsReceiver)
            {
                return standardsManager.TrustDriver.GetBinaryNegotiation(this);
            }
            else
            {
                return negotiationData;
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

            this.authenticator = Fx.AllocateByteArray(authenticator.Length);
            Buffer.BlockCopy(authenticator, 0, this.authenticator, 0, authenticator.Length);
        }

        internal byte[] GetAuthenticator()
        {
            if (IsReceiver)
            {
                return standardsManager.TrustDriver.GetAuthenticator(this);
            }
            else
            {
                if (authenticator == null)
                {
                    return null;
                }
                else
                {
                    byte[] result = Fx.AllocateByteArray(authenticator.Length);
                    Buffer.BlockCopy(authenticator, 0, result, 0, authenticator.Length);
                    return result;
                }
            }
        }

        private void OnWriteTo(XmlWriter w)
        {
            if (IsReceiver)
            {
                rstrXml.WriteTo(w);
            }
            else
            {
                standardsManager.TrustDriver.WriteRequestSecurityTokenResponse(this, w);
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
                if (cachedWriteBuffer == null)
                {
                    MemoryStream stream = new MemoryStream();
                    using (XmlDictionaryWriter binaryWriter = XmlDictionaryWriter.CreateBinaryWriter(stream, XD.Dictionary))
                    {
                        OnWriteTo(binaryWriter);
                        binaryWriter.Flush();
                        stream.Flush();
                        stream.Seek(0, SeekOrigin.Begin);
                        cachedWriteBuffer = stream.GetBuffer();
                        cachedWriteBufferLength = (int)stream.Length;
                    }
                }
                writer.WriteNode(XmlDictionaryReader.CreateBinaryReader(cachedWriteBuffer, 0, cachedWriteBufferLength, XD.Dictionary, XmlDictionaryReaderQuotas.Max), false);
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
            if (!isReadOnly)
            {
                isReadOnly = true;
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

            return standardsManager.TrustDriver.GetIssuedToken(this, resolver, allowedAuthenticators, keyEntropyMode, requestorEntropy, expectedTokenType, authorizationPolicies, defaultKeySize, isBearerKeyType);
        }

        public virtual GenericXmlSecurityToken GetIssuedToken(string expectedTokenType, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, RSA clientKey)
        {
            if (!IsReceiver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTROnly, nameof(GetIssuedToken))));
            }

            return standardsManager.TrustDriver.GetIssuedToken(this, expectedTokenType, authorizationPolicies, clientKey);
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
            if ((keySizeInBits < minSaneKeySizeInBits) || (keySizeInBits > maxSaneKeySizeInBits))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.InvalidKeySizeSpecifiedInNegotiation, keySizeInBits, minSaneKeySizeInBits, maxSaneKeySizeInBits)));
            }

            Psha1DerivedKeyGenerator generator = new Psha1DerivedKeyGenerator(requestorEntropy);
            return generator.GenerateDerivedKey(new byte[] { }, issuerEntropy, keySizeInBits, 0);
        }
    }
}
