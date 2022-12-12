// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;
using HexBinary = CoreWCF.Security.SoapHexBinary;
using TokenEntry = CoreWCF.Security.WSSecurityTokenSerializer.TokenEntry;

namespace CoreWCF.Security
{
    internal abstract class WSTrust : WSSecurityTokenSerializer.SerializerEntries
    {
        public WSTrust(WSSecurityTokenSerializer tokenSerializer)
        {
            WSSecurityTokenSerializer = tokenSerializer;
        }

        public WSSecurityTokenSerializer WSSecurityTokenSerializer { get; }

        public abstract TrustDictionary SerializerDictionary
        {
            get;
        }

        public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
        {
            tokenEntryList.Add(new BinarySecretTokenEntry(this));
        }

        private class BinarySecretTokenEntry : TokenEntry
        {
            private readonly WSTrust _parent;
            private readonly TrustDictionary _otherDictionary;

            public BinarySecretTokenEntry(WSTrust parent)
            {
                _parent = parent;
                _otherDictionary = null;

                if (parent.SerializerDictionary is TrustDec2005Dictionary)
                {
                    _otherDictionary = XD.TrustFeb2005Dictionary;
                }

                if (parent.SerializerDictionary is TrustFeb2005Dictionary)
                {
                    _otherDictionary = DXD.TrustDec2005Dictionary;
                }

                // always set it, so we don't have to worry about null
                if (_otherDictionary == null)
                {
                    _otherDictionary = _parent.SerializerDictionary;
                }
            }

            protected override XmlDictionaryString LocalName => _parent.SerializerDictionary.BinarySecret;
            protected override XmlDictionaryString NamespaceUri => _parent.SerializerDictionary.Namespace;
            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(BinarySecretSecurityToken) }; }
            public override string TokenTypeUri => null;
            protected override string ValueTypeUri => null;

            public override bool CanReadTokenCore(XmlElement element)
            {
                string valueTypeUri = null;

                if (element.HasAttribute(SecurityJan2004Strings.ValueType, null))
                {
                    valueTypeUri = element.GetAttribute(SecurityJan2004Strings.ValueType, null);
                }

                return element.LocalName == LocalName.Value && (element.NamespaceURI == NamespaceUri.Value || element.NamespaceURI == _otherDictionary.Namespace.Value) && valueTypeUri == ValueTypeUri;
            }

            public override bool CanReadTokenCore(XmlDictionaryReader reader)
            {
                return (reader.IsStartElement(LocalName, NamespaceUri) || reader.IsStartElement(LocalName, _otherDictionary.Namespace)) &&
                       reader.GetAttribute(XD.SecurityJan2004Dictionary.ValueType, null) == ValueTypeUri;
            }


            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml,
                SecurityTokenReferenceStyle tokenReferenceStyle)
            {
                TokenReferenceStyleHelper.Validate(tokenReferenceStyle);

                switch (tokenReferenceStyle)
                {
                    case SecurityTokenReferenceStyle.Internal:
                        return CreateDirectReference(issuedTokenXml, UtilityStrings.IdAttribute, UtilityStrings.Namespace, typeof(GenericXmlSecurityToken));
                    case SecurityTokenReferenceStyle.External:
                        // Binary Secret tokens aren't referred to externally
                        return null;
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(tokenReferenceStyle)));
                }
            }

            public override SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                string secretType = reader.GetAttribute(XD.SecurityJan2004Dictionary.TypeAttribute, null);
                string id = reader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
                bool isNonce = false;

                if (secretType != null && secretType.Length > 0)
                {
                    if (secretType == _parent.SerializerDictionary.NonceBinarySecret.Value || secretType == _otherDictionary.NonceBinarySecret.Value)
                    {
                        isNonce = true;
                    }
                    else if (secretType != _parent.SerializerDictionary.SymmetricKeyBinarySecret.Value && secretType != _otherDictionary.SymmetricKeyBinarySecret.Value)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.UnexpectedBinarySecretType, _parent.SerializerDictionary.SymmetricKeyBinarySecret.Value, secretType)));
                    }
                }

                byte[] secret = reader.ReadElementContentAsBase64();
                if (isNonce)
                {
                    return new NonceToken(id, secret);
                }
                else
                {
                    return new BinarySecretSecurityToken(id, secret);
                }
            }

            public override void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token)
            {
                BinarySecretSecurityToken simpleToken = token as BinarySecretSecurityToken;
                byte[] secret = simpleToken.GetKeyBytes();
                writer.WriteStartElement(_parent.SerializerDictionary.Prefix.Value, _parent.SerializerDictionary.BinarySecret, _parent.SerializerDictionary.Namespace);
                if (simpleToken.Id != null)
                {
                    writer.WriteAttributeString(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace, simpleToken.Id);
                }
                if (token is NonceToken)
                {
                    writer.WriteAttributeString(XD.SecurityJan2004Dictionary.TypeAttribute, null, _parent.SerializerDictionary.NonceBinarySecret.Value);
                }
                writer.WriteBase64(secret, 0, secret.Length);
                writer.WriteEndElement();
            }
        }

        public abstract class Driver : TrustDriver
        {
            private const string Base64Uri = SecurityJan2004Strings.EncodingTypeValueBase64Binary;
            private const string HexBinaryUri = SecurityJan2004Strings.EncodingTypeValueHexBinary;
            private readonly SecurityStandardsManager _standardsManager;
            private readonly List<SecurityTokenAuthenticator> _entropyAuthenticators;

            public Driver(SecurityStandardsManager standardsManager)
            {
                _standardsManager = standardsManager;
                _entropyAuthenticators = new List<SecurityTokenAuthenticator>(2);
            }

            public abstract TrustDictionary DriverDictionary
            {
                get;
            }

            public override XmlDictionaryString RequestSecurityTokenAction => DriverDictionary.RequestSecurityTokenIssuance;

            public override XmlDictionaryString RequestSecurityTokenResponseAction => DriverDictionary.RequestSecurityTokenIssuanceResponse;

            public override string RequestTypeIssue => DriverDictionary.RequestTypeIssue.Value;

            public override string ComputedKeyAlgorithm => DriverDictionary.Psha1ComputedKeyUri.Value;

            public override SecurityStandardsManager StandardsManager => _standardsManager;

            public override XmlDictionaryString Namespace => DriverDictionary.Namespace;

            public override RequestSecurityToken CreateRequestSecurityToken(XmlReader xmlReader)
            {
                XmlDictionaryReader reader = XmlDictionaryReader.CreateDictionaryReader(xmlReader);
                reader.MoveToStartElement(DriverDictionary.RequestSecurityToken, DriverDictionary.Namespace);
                string context = null;
                string tokenTypeUri = null;
                string requestType = null;
                int keySize = 0;
                XmlDocument doc = new XmlDocument();
                XmlElement rstXml = (doc.ReadNode(reader) as XmlElement);
                for (int i = 0; i < rstXml.Attributes.Count; ++i)
                {
                    XmlAttribute attr = rstXml.Attributes[i];
                    if (attr.LocalName == DriverDictionary.Context.Value)
                    {
                        context = attr.Value;
                    }
                }
                for (int i = 0; i < rstXml.ChildNodes.Count; ++i)
                {
                    XmlElement child = (rstXml.ChildNodes[i] as XmlElement);
                    if (child != null)
                    {
                        if (child.LocalName == DriverDictionary.TokenType.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            tokenTypeUri = XmlHelper.ReadTextElementAsTrimmedString(child);
                        }
                        else if (child.LocalName == DriverDictionary.RequestType.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            requestType = XmlHelper.ReadTextElementAsTrimmedString(child);
                        }
                        else if (child.LocalName == DriverDictionary.KeySize.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            keySize = int.Parse(XmlHelper.ReadTextElementAsTrimmedString(child), NumberFormatInfo.InvariantInfo);
                        }
                    }
                }

                ReadTargets(rstXml, out SecurityKeyIdentifierClause renewTarget, out SecurityKeyIdentifierClause closeTarget);

                RequestSecurityToken rst = new RequestSecurityToken(_standardsManager, rstXml, context, tokenTypeUri, requestType, keySize, renewTarget, closeTarget);
                return rst;
            }

            private CoreWCF.XmlBuffer GetIssuedTokenBuffer(CoreWCF.XmlBuffer rstrBuffer)
            {
                CoreWCF.XmlBuffer issuedTokenBuffer = null;
                using (XmlDictionaryReader reader = rstrBuffer.GetReader(0))
                {
                    reader.ReadFullStartElement();
                    while (reader.IsStartElement())
                    {
                        if (reader.IsStartElement(DriverDictionary.RequestedSecurityToken, DriverDictionary.Namespace))
                        {
                            reader.ReadStartElement();
                            reader.MoveToContent();
                            issuedTokenBuffer = new CoreWCF.XmlBuffer(int.MaxValue);
                            using (XmlDictionaryWriter writer = issuedTokenBuffer.OpenSection(reader.Quotas))
                            {
                                writer.WriteNode(reader, false);
                                issuedTokenBuffer.CloseSection();
                                issuedTokenBuffer.Close();
                            }
                            reader.ReadEndElement();
                            break;
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }
                return issuedTokenBuffer;
            }

            public override RequestSecurityTokenResponse CreateRequestSecurityTokenResponse(XmlReader xmlReader)
            {
                if (xmlReader == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xmlReader));
                }
                XmlDictionaryReader reader = XmlDictionaryReader.CreateDictionaryReader(xmlReader);
                if (reader.IsStartElement(DriverDictionary.RequestSecurityTokenResponse, DriverDictionary.Namespace) == false)
                {
                    XmlHelper.OnRequiredElementMissing(DriverDictionary.RequestSecurityTokenResponse.Value, DriverDictionary.Namespace.Value);
                }

                CoreWCF.XmlBuffer rstrBuffer = new CoreWCF.XmlBuffer(int.MaxValue);
                using (XmlDictionaryWriter writer = rstrBuffer.OpenSection(reader.Quotas))
                {
                    writer.WriteNode(reader, false);
                    rstrBuffer.CloseSection();
                    rstrBuffer.Close();
                }
                XmlDocument doc = new XmlDocument();
                XmlElement rstrXml;
                using (XmlReader reader2 = rstrBuffer.GetReader(0))
                {
                    rstrXml = (doc.ReadNode(reader2) as XmlElement);
                }

                CoreWCF.XmlBuffer issuedTokenBuffer = GetIssuedTokenBuffer(rstrBuffer);
                string context = null;
                string tokenTypeUri = null;
                int keySize = 0;
                bool computeKey = false;
                DateTime created = DateTime.UtcNow;
                DateTime expires = SecurityUtils.MaxUtcDateTime;
                for (int i = 0; i < rstrXml.Attributes.Count; ++i)
                {
                    XmlAttribute attr = rstrXml.Attributes[i];
                    if (attr.LocalName == DriverDictionary.Context.Value)
                    {
                        context = attr.Value;
                    }
                }

                for (int i = 0; i < rstrXml.ChildNodes.Count; ++i)
                {
                    XmlElement child = (rstrXml.ChildNodes[i] as XmlElement);
                    if (child != null)
                    {
                        if (child.LocalName == DriverDictionary.TokenType.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            tokenTypeUri = XmlHelper.ReadTextElementAsTrimmedString(child);
                        }
                        else if (child.LocalName == DriverDictionary.KeySize.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            keySize = int.Parse(XmlHelper.ReadTextElementAsTrimmedString(child), NumberFormatInfo.InvariantInfo);
                        }
                        else if (child.LocalName == DriverDictionary.RequestedProofToken.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            XmlElement proofXml = XmlHelper.GetChildElement(child);
                            if (proofXml.LocalName == DriverDictionary.ComputedKey.Value && proofXml.NamespaceURI == DriverDictionary.Namespace.Value)
                            {
                                string computedKeyAlgorithm = XmlHelper.ReadTextElementAsTrimmedString(proofXml);
                                if (computedKeyAlgorithm != DriverDictionary.Psha1ComputedKeyUri.Value)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.UnknownComputedKeyAlgorithm, computedKeyAlgorithm)));
                                }
                                computeKey = true;
                            }
                        }
                        else if (child.LocalName == DriverDictionary.Lifetime.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            XmlElement createdXml = XmlHelper.GetChildElement(child, UtilityStrings.CreatedElement, UtilityStrings.Namespace);
                            if (createdXml != null)
                            {
                                created = DateTime.ParseExact(XmlHelper.ReadTextElementAsTrimmedString(createdXml),
                                    WSUtilitySpecificationVersion.AcceptedDateTimeFormats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None).ToUniversalTime();
                            }
                            XmlElement expiresXml = XmlHelper.GetChildElement(child, UtilityStrings.ExpiresElement, UtilityStrings.Namespace);
                            if (expiresXml != null)
                            {
                                expires = DateTime.ParseExact(XmlHelper.ReadTextElementAsTrimmedString(expiresXml),
                                    WSUtilitySpecificationVersion.AcceptedDateTimeFormats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None).ToUniversalTime();
                            }
                        }
                    }
                }

                bool isRequestedTokenClosed = ReadRequestedTokenClosed(rstrXml);
                ReadReferences(rstrXml, out SecurityKeyIdentifierClause requestedAttachedReference, out SecurityKeyIdentifierClause requestedUnattachedReference);

                return new RequestSecurityTokenResponse(_standardsManager, rstrXml, context, tokenTypeUri, keySize, requestedAttachedReference, requestedUnattachedReference,
                                                        computeKey, created, expires, isRequestedTokenClosed, issuedTokenBuffer);
            }

            public override RequestSecurityTokenResponseCollection CreateRequestSecurityTokenResponseCollection(XmlReader xmlReader)
            {
                XmlDictionaryReader reader = XmlDictionaryReader.CreateDictionaryReader(xmlReader);
                List<RequestSecurityTokenResponse> rstrCollection = new List<RequestSecurityTokenResponse>(2);
                string rootName = reader.Name;
                reader.ReadStartElement(DriverDictionary.RequestSecurityTokenResponseCollection, DriverDictionary.Namespace);
                while (reader.IsStartElement(DriverDictionary.RequestSecurityTokenResponse.Value, DriverDictionary.Namespace.Value))
                {
                    RequestSecurityTokenResponse rstr = CreateRequestSecurityTokenResponse(reader);
                    rstrCollection.Add(rstr);
                }
                reader.ReadEndElement();
                if (rstrCollection.Count == 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.NoRequestSecurityTokenResponseElements)));
                }

                return new RequestSecurityTokenResponseCollection(rstrCollection.AsReadOnly(), StandardsManager);
            }

            private XmlElement GetAppliesToElement(XmlElement rootElement)
            {
                if (rootElement == null)
                {
                    return null;
                }
                for (int i = 0; i < rootElement.ChildNodes.Count; ++i)
                {
                    XmlElement elem = (rootElement.ChildNodes[i] as XmlElement);
                    if (elem != null)
                    {
                        if (elem.LocalName == DriverDictionary.AppliesTo.Value && elem.NamespaceURI == Namespaces.WSPolicy)
                        {
                            return elem;
                        }
                    }
                }
                return null;
            }

            private T GetAppliesTo<T>(XmlElement rootXml, XmlObjectSerializer serializer)
            {
                XmlElement appliesToElement = GetAppliesToElement(rootXml);
                if (appliesToElement != null)
                {
                    using (XmlReader reader = new XmlNodeReader(appliesToElement))
                    {
                        reader.ReadStartElement();
                        lock (serializer)
                        {
                            return (T)serializer.ReadObject(reader);
                        }
                    }
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.NoAppliesToPresent)));
                }
            }

            public override T GetAppliesTo<T>(RequestSecurityToken rst, XmlObjectSerializer serializer)
            {
                if (rst == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rst));
                }

                return GetAppliesTo<T>(rst.RequestSecurityTokenXml, serializer);
            }

            public override T GetAppliesTo<T>(RequestSecurityTokenResponse rstr, XmlObjectSerializer serializer)
            {
                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstr));
                }

                return GetAppliesTo<T>(rstr.RequestSecurityTokenResponseXml, serializer);
            }

            public override bool IsAppliesTo(string localName, string namespaceUri)
            {
                return (localName == DriverDictionary.AppliesTo.Value && namespaceUri == Namespaces.WSPolicy);
            }

            private void GetAppliesToQName(XmlElement rootElement, out string localName, out string namespaceUri)
            {
                localName = namespaceUri = null;
                XmlElement appliesToElement = GetAppliesToElement(rootElement);
                if (appliesToElement != null)
                {
                    using (XmlReader reader = new XmlNodeReader(appliesToElement))
                    {
                        reader.ReadStartElement();
                        reader.MoveToContent();
                        localName = reader.LocalName;
                        namespaceUri = reader.NamespaceURI;
                    }
                }
            }

            public override void GetAppliesToQName(RequestSecurityToken rst, out string localName, out string namespaceUri)
            {
                if (rst == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rst));
                }

                GetAppliesToQName(rst.RequestSecurityTokenXml, out localName, out namespaceUri);
            }

            public override void GetAppliesToQName(RequestSecurityTokenResponse rstr, out string localName, out string namespaceUri)
            {
                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstr));
                }

                GetAppliesToQName(rstr.RequestSecurityTokenResponseXml, out localName, out namespaceUri);
            }

            public override byte[] GetAuthenticator(RequestSecurityTokenResponse rstr)
            {
                if (rstr != null && rstr.RequestSecurityTokenResponseXml != null && rstr.RequestSecurityTokenResponseXml.ChildNodes != null)
                {
                    for (int i = 0; i < rstr.RequestSecurityTokenResponseXml.ChildNodes.Count; ++i)
                    {
                        if (rstr.RequestSecurityTokenResponseXml.ChildNodes[i] is XmlElement element)
                        {
                            if (element.LocalName == DriverDictionary.Authenticator.Value && element.NamespaceURI == DriverDictionary.Namespace.Value)
                            {
                                XmlElement combinedHashElement = XmlHelper.GetChildElement(element);
                                if (combinedHashElement.LocalName == DriverDictionary.CombinedHash.Value && combinedHashElement.NamespaceURI == DriverDictionary.Namespace.Value)
                                {
                                    string authenticatorString = XmlHelper.ReadTextElementAsTrimmedString(combinedHashElement);
                                    return Convert.FromBase64String(authenticatorString);
                                }
                            }
                        }
                    }
                }
                return null;
            }

            public override BinaryNegotiation GetBinaryNegotiation(RequestSecurityTokenResponse rstr)
            {
                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstr));
                }

                return GetBinaryNegotiation(rstr.RequestSecurityTokenResponseXml);
            }

            public override BinaryNegotiation GetBinaryNegotiation(RequestSecurityToken rst)
            {
                if (rst == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rst));
                }

                return GetBinaryNegotiation(rst.RequestSecurityTokenXml);
            }

            private BinaryNegotiation GetBinaryNegotiation(XmlElement rootElement)
            {
                if (rootElement == null)
                {
                    return null;
                }
                for (int i = 0; i < rootElement.ChildNodes.Count; ++i)
                {
                    if (rootElement.ChildNodes[i] is XmlElement elem)
                    {
                        if (elem.LocalName == DriverDictionary.BinaryExchange.Value && elem.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            return ReadBinaryNegotiation(elem);
                        }
                    }
                }
                return null;
            }

            public override SecurityToken GetEntropy(RequestSecurityToken rst, SecurityTokenResolver resolver)
            {
                if (rst == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rst));
                }

                return GetEntropy(rst.RequestSecurityTokenXml, resolver);
            }

            public override SecurityToken GetEntropy(RequestSecurityTokenResponse rstr, SecurityTokenResolver resolver)
            {
                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstr));
                }

                return GetEntropy(rstr.RequestSecurityTokenResponseXml, resolver);
            }

            private SecurityToken GetEntropy(XmlElement rootElement, SecurityTokenResolver resolver)
            {
                if (rootElement == null || rootElement.ChildNodes == null)
                {
                    return null;
                }
                for (int i = 0; i < rootElement.ChildNodes.Count; ++i)
                {
                    if (rootElement.ChildNodes[i] is XmlElement element)
                    {
                        if (element.LocalName == DriverDictionary.Entropy.Value && element.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            XmlElement tokenXml = XmlHelper.GetChildElement(element);
                            string valueTypeUri = element.GetAttribute(SecurityJan2004Strings.ValueType);
                            if (valueTypeUri.Length == 0)
                            {
                            }

                            return _standardsManager.SecurityTokenSerializer.ReadToken(new XmlNodeReader(tokenXml), resolver);
                        }
                    }
                }
                return null;
            }

            private void GetIssuedAndProofXml(RequestSecurityTokenResponse rstr, out XmlElement issuedTokenXml, out XmlElement proofTokenXml)
            {
                issuedTokenXml = null;
                proofTokenXml = null;
                if ((rstr.RequestSecurityTokenResponseXml != null) && (rstr.RequestSecurityTokenResponseXml.ChildNodes != null))
                {
                    for (int i = 0; i < rstr.RequestSecurityTokenResponseXml.ChildNodes.Count; ++i)
                    {
                        if (rstr.RequestSecurityTokenResponseXml.ChildNodes[i] is XmlElement elem)
                        {
                            if (elem.LocalName == DriverDictionary.RequestedSecurityToken.Value && elem.NamespaceURI == DriverDictionary.Namespace.Value)
                            {
                                if (issuedTokenXml != null)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.RstrHasMultipleIssuedTokens)));
                                }
                                issuedTokenXml = XmlHelper.GetChildElement(elem);
                            }
                            else if (elem.LocalName == DriverDictionary.RequestedProofToken.Value && elem.NamespaceURI == DriverDictionary.Namespace.Value)
                            {
                                if (proofTokenXml != null)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.RstrHasMultipleProofTokens)));
                                }
                                proofTokenXml = XmlHelper.GetChildElement(elem);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// The algorithm for computing the key is:
            /// 1. If there is requestorEntropy:
            ///    a. If there is no <RequestedProofToken> use the requestorEntropy as the key
            ///    b. If there is a <RequestedProofToken> with a ComputedKeyUri, combine the client and server entropies
            ///    c. Anything else, throw
            /// 2. If there is no requestorEntropy:
            ///    a. THere has to be a <RequestedProofToken> that contains the proof key
            /// </summary>
            public override GenericXmlSecurityToken GetIssuedToken(RequestSecurityTokenResponse rstr, SecurityTokenResolver resolver, IList<SecurityTokenAuthenticator> allowedAuthenticators, SecurityKeyEntropyMode keyEntropyMode, byte[] requestorEntropy, string expectedTokenType,
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, int defaultKeySize, bool isBearerKeyType)
            {
                SecurityKeyEntropyModeHelper.Validate(keyEntropyMode);

                if (defaultKeySize < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(defaultKeySize), SRCommon.ValueMustBeNonNegative));
                }

                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstr));
                }

                string tokenType;
                if (rstr.TokenType != null)
                {
                    if (expectedTokenType != null && expectedTokenType != rstr.TokenType)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BadIssuedTokenType, rstr.TokenType, expectedTokenType)));
                    }
                    tokenType = rstr.TokenType;
                }
                else
                {
                }

                // search the response elements for licenseXml, proofXml, and lifetime
                DateTime created = rstr.ValidFrom;
                DateTime expires = rstr.ValidTo;
                GetIssuedAndProofXml(rstr, out XmlElement issuedTokenXml, out XmlElement proofXml);

                if (issuedTokenXml == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.NoLicenseXml)));
                }

                if (isBearerKeyType)
                {
                    if (proofXml != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BearerKeyTypeCannotHaveProofKey)));
                    }

                    return new GenericXmlSecurityToken(issuedTokenXml, null, created, expires, rstr.RequestedAttachedReference, rstr.RequestedUnattachedReference, authorizationPolicies);
                }

                SecurityToken proofToken;
                SecurityToken entropyToken = GetEntropy(rstr, resolver);
                if (keyEntropyMode == SecurityKeyEntropyMode.ClientEntropy)
                {
                    if (requestorEntropy == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeRequiresRequestorEntropy, keyEntropyMode)));
                    }
                    // enforce that there is no entropy or proof token in the RSTR
                    if (proofXml != null || entropyToken != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeCannotHaveProofTokenOrIssuerEntropy, keyEntropyMode)));
                    }
                    proofToken = new BinarySecretSecurityToken(requestorEntropy);
                }
                else if (keyEntropyMode == SecurityKeyEntropyMode.ServerEntropy)
                {
                    if (requestorEntropy != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeCannotHaveRequestorEntropy, keyEntropyMode)));
                    }
                    if (rstr.ComputeKey || entropyToken != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeCannotHaveComputedKey, keyEntropyMode)));
                    }
                    if (proofXml == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeRequiresProofToken, keyEntropyMode)));
                    }
                    string valueTypeUri = proofXml.GetAttribute(SecurityJan2004Strings.ValueType);
                    if (valueTypeUri.Length == 0)
                    {
                    }

                    proofToken = _standardsManager.SecurityTokenSerializer.ReadToken(new XmlNodeReader(proofXml), resolver);
                }
                else
                {
                    if (!rstr.ComputeKey)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeRequiresComputedKey, keyEntropyMode)));
                    }
                    if (entropyToken == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeRequiresIssuerEntropy, keyEntropyMode)));
                    }
                    if (requestorEntropy == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EntropyModeRequiresRequestorEntropy, keyEntropyMode)));
                    }
                    if (rstr.KeySize == 0 && defaultKeySize == 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.RstrKeySizeNotProvided)));
                    }
                    int issuedKeySize = (rstr.KeySize != 0) ? rstr.KeySize : defaultKeySize;
                    byte[] issuerEntropy;
                    if (entropyToken is BinarySecretSecurityToken)
                    {
                        issuerEntropy = ((BinarySecretSecurityToken)entropyToken).GetKeyBytes();
                    }
                    else if (entropyToken is WrappedKeySecurityToken)
                    {
                        issuerEntropy = ((WrappedKeySecurityToken)entropyToken).GetWrappedKey();
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedIssuerEntropyType)));
                    }
                    // compute the PSHA1 derived key
                    byte[] issuedKey = RequestSecurityTokenResponse.ComputeCombinedKey(requestorEntropy, issuerEntropy, issuedKeySize);
                    proofToken = new BinarySecretSecurityToken(issuedKey);
                }

                SecurityKeyIdentifierClause internalReference = rstr.RequestedAttachedReference;
                SecurityKeyIdentifierClause externalReference = rstr.RequestedUnattachedReference;

                return new BufferedGenericXmlSecurityToken(issuedTokenXml, proofToken, created, expires, internalReference, externalReference, authorizationPolicies, rstr.IssuedTokenBuffer);
            }

            public override GenericXmlSecurityToken GetIssuedToken(RequestSecurityTokenResponse rstr, string expectedTokenType,
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, RSA clientKey)
            {
                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(rstr)));
                }

                string tokenType;
                if (rstr.TokenType != null)
                {
                    if (expectedTokenType != null && expectedTokenType != rstr.TokenType)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BadIssuedTokenType, rstr.TokenType, expectedTokenType)));
                    }
                    tokenType = rstr.TokenType;
                }
                else
                {
                }

                // search the response elements for licenseXml, proofXml, and lifetime
                DateTime created = rstr.ValidFrom;
                DateTime expires = rstr.ValidTo;
                GetIssuedAndProofXml(rstr, out XmlElement issuedTokenXml, out XmlElement proofXml);

                if (issuedTokenXml == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.NoLicenseXml)));
                }

                // enforce that there is no proof token in the RSTR
                if (proofXml != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProofTokenXmlUnexpectedInRstr)));
                }
                SecurityKeyIdentifierClause internalReference = rstr.RequestedAttachedReference;
                SecurityKeyIdentifierClause externalReference = rstr.RequestedUnattachedReference;

                SecurityToken proofToken = new RsaSecurityToken(clientKey);
                return new BufferedGenericXmlSecurityToken(issuedTokenXml, proofToken, created, expires, internalReference, externalReference, authorizationPolicies, rstr.IssuedTokenBuffer);
            }

            public override bool IsAtRequestSecurityTokenResponse(XmlReader reader)
            {
                if (reader == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
                }

                return reader.IsStartElement(DriverDictionary.RequestSecurityTokenResponse.Value, DriverDictionary.Namespace.Value);
            }

            public override bool IsAtRequestSecurityTokenResponseCollection(XmlReader reader)
            {
                if (reader == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
                }

                return reader.IsStartElement(DriverDictionary.RequestSecurityTokenResponseCollection.Value, DriverDictionary.Namespace.Value);
            }

            public override bool IsRequestedSecurityTokenElement(string name, string nameSpace)
            {
                return (name == DriverDictionary.RequestedSecurityToken.Value && nameSpace == DriverDictionary.Namespace.Value);
            }

            public override bool IsRequestedProofTokenElement(string name, string nameSpace)
            {
                return (name == DriverDictionary.RequestedProofToken.Value && nameSpace == DriverDictionary.Namespace.Value);
            }

            public static BinaryNegotiation ReadBinaryNegotiation(XmlElement elem)
            {
                if (elem == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elem));
                }

                // get the encoding and valueType attributes
                string encodingUri = null;
                string valueTypeUri = null;
                if (elem.Attributes != null)
                {
                    for (int i = 0; i < elem.Attributes.Count; ++i)
                    {
                        XmlAttribute attr = elem.Attributes[i];
                        if (attr.LocalName == SecurityJan2004Strings.EncodingType && attr.NamespaceURI.Length == 0)
                        {
                            encodingUri = attr.Value;
                            if (encodingUri != Base64Uri && encodingUri != HexBinaryUri)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnsupportedBinaryEncoding, encodingUri)));
                            }
                        }
                        else if (attr.LocalName == SecurityJan2004Strings.ValueType && attr.NamespaceURI.Length == 0)
                        {
                            valueTypeUri = attr.Value;
                        }
                        // ignore all other attributes
                    }
                }
                if (encodingUri == null)
                {
                    XmlHelper.OnRequiredAttributeMissing("EncodingType", elem.Name);
                }
                if (valueTypeUri == null)
                {
                    XmlHelper.OnRequiredAttributeMissing("ValueType", elem.Name);
                }
                string encodedBlob = XmlHelper.ReadTextElementAsTrimmedString(elem);
                byte[] negotiationData;
                if (encodingUri == Base64Uri)
                {
                    negotiationData = Convert.FromBase64String(encodedBlob);
                }
                else
                {
                    negotiationData = HexBinary.Parse(encodedBlob).Value;
                }
                return new BinaryNegotiation(valueTypeUri, negotiationData);
            }

            // Note in Apr2004, internal & external references aren't supported - 
            // our strategy is to see if there's a token reference (and use it for external ref) and backup is to scan the token xml to compute reference
            protected virtual void ReadReferences(XmlElement rstrXml, out SecurityKeyIdentifierClause requestedAttachedReference,
                    out SecurityKeyIdentifierClause requestedUnattachedReference)
            {
                XmlElement issuedTokenXml = null;
                requestedAttachedReference = null;
                requestedUnattachedReference = null;
                for (int i = 0; i < rstrXml.ChildNodes.Count; ++i)
                {
                    if (rstrXml.ChildNodes[i] is XmlElement child)
                    {
                        if (child.LocalName == DriverDictionary.RequestedSecurityToken.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            issuedTokenXml = XmlHelper.GetChildElement(child);
                        }
                        else if (child.LocalName == DriverDictionary.RequestedTokenReference.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            requestedUnattachedReference = GetKeyIdentifierXmlReferenceClause(XmlHelper.GetChildElement(child));
                        }
                    }
                }

                if (issuedTokenXml != null)
                {
                    requestedAttachedReference = _standardsManager.CreateKeyIdentifierClauseFromTokenXml(issuedTokenXml, SecurityTokenReferenceStyle.Internal);
                    if (requestedUnattachedReference == null)
                    {
                        try
                        {
                            requestedUnattachedReference = _standardsManager.CreateKeyIdentifierClauseFromTokenXml(issuedTokenXml, SecurityTokenReferenceStyle.External);
                        }
                        catch (XmlException)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.TrustDriverIsUnableToCreatedNecessaryAttachedOrUnattachedReferences, issuedTokenXml.ToString())));
                        }
                    }
                }
            }

            internal bool TryReadKeyIdentifierClause(XmlNodeReader reader, out SecurityKeyIdentifierClause keyIdentifierClause)
            {
                try
                {
                    keyIdentifierClause = _standardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(reader);
                }
                catch (XmlException e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    keyIdentifierClause = null;
                    return false;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    keyIdentifierClause = null;
                    return false;
                }

                return true;
            }

            internal SecurityKeyIdentifierClause CreateGenericXmlSecurityKeyIdentifierClause(XmlNodeReader reader, XmlElement keyIdentifierReferenceXmlElement)
            {
                XmlDictionaryReader localReader = XmlDictionaryReader.CreateDictionaryReader(reader);
                string strId = localReader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
                SecurityKeyIdentifierClause keyIdentifierClause = new GenericXmlSecurityKeyIdentifierClause(keyIdentifierReferenceXmlElement);
                if (!string.IsNullOrEmpty(strId))
                {
                    keyIdentifierClause.Id = strId;
                }
                return keyIdentifierClause;
            }

            internal SecurityKeyIdentifierClause GetKeyIdentifierXmlReferenceClause(XmlElement keyIdentifierReferenceXmlElement)
            {
                XmlNodeReader reader = new XmlNodeReader(keyIdentifierReferenceXmlElement);
                if (!TryReadKeyIdentifierClause(reader, out SecurityKeyIdentifierClause keyIdentifierClause))
                {
                    keyIdentifierClause = CreateGenericXmlSecurityKeyIdentifierClause(new XmlNodeReader(keyIdentifierReferenceXmlElement), keyIdentifierReferenceXmlElement);
                }

                return keyIdentifierClause;
            }

            protected virtual bool ReadRequestedTokenClosed(XmlElement rstrXml)
            {
                return false;
            }

            protected virtual void ReadTargets(XmlElement rstXml, out SecurityKeyIdentifierClause renewTarget, out SecurityKeyIdentifierClause closeTarget)
            {
                renewTarget = null;
                closeTarget = null;
            }

            public override void OnRSTRorRSTRCMissingException()
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ExpectedOneOfTwoElementsFromNamespace,
                    DriverDictionary.RequestSecurityTokenResponse, DriverDictionary.RequestSecurityTokenResponseCollection,
                    DriverDictionary.Namespace)));
            }

            private void WriteAppliesTo(object appliesTo, Type appliesToType, XmlObjectSerializer serializer, XmlWriter xmlWriter)
            {
                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter);
                writer.WriteStartElement(Namespaces.WSPolicyPrefix, DriverDictionary.AppliesTo.Value, Namespaces.WSPolicy);
                lock (serializer)
                {
                    serializer.WriteObject(writer, appliesTo);
                }
                writer.WriteEndElement();
            }

            public void WriteBinaryNegotiation(BinaryNegotiation negotiation, XmlWriter xmlWriter)
            {
                if (negotiation == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiation));
                }

                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter);
                negotiation.WriteTo(writer, DriverDictionary.Prefix.Value,
                                            DriverDictionary.BinaryExchange, DriverDictionary.Namespace,
                                            XD.SecurityJan2004Dictionary.ValueType, null);
            }

            public override void WriteRequestSecurityToken(RequestSecurityToken rst, XmlWriter xmlWriter)
            {
                if (rst == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rst));
                }
                if (xmlWriter == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xmlWriter));
                }
                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter);
                if (rst.IsReceiver)
                {
                    rst.WriteTo(writer);
                    return;
                }
                writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestSecurityToken, DriverDictionary.Namespace);
                XmlHelper.AddNamespaceDeclaration(writer, DriverDictionary.Prefix.Value, DriverDictionary.Namespace);
                if (rst.Context != null)
                {
                    writer.WriteAttributeString(DriverDictionary.Context, null, rst.Context);
                }

                rst.OnWriteCustomAttributes(writer);
                if (rst.TokenType != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.TokenType, DriverDictionary.Namespace);
                    writer.WriteString(rst.TokenType);
                    writer.WriteEndElement();
                }
                if (rst.RequestType != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestType, DriverDictionary.Namespace);
                    writer.WriteString(rst.RequestType);
                    writer.WriteEndElement();
                }

                if (rst.AppliesTo != null)
                {
                    WriteAppliesTo(rst.AppliesTo, rst.AppliesToType, rst.AppliesToSerializer, writer);
                }

                SecurityToken entropyToken = rst.GetRequestorEntropy();
                if (entropyToken != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.Entropy, DriverDictionary.Namespace);
                    _standardsManager.SecurityTokenSerializer.WriteToken(writer, entropyToken);
                    writer.WriteEndElement();
                }

                if (rst.KeySize != 0)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.KeySize, DriverDictionary.Namespace);
                    writer.WriteValue(rst.KeySize);
                    writer.WriteEndElement();
                }

                BinaryNegotiation negotiationData = rst.GetBinaryNegotiation();
                if (negotiationData != null)
                {
                    WriteBinaryNegotiation(negotiationData, writer);
                }

                WriteTargets(rst, writer);

                if (rst.RequestProperties != null)
                {
                    foreach (XmlElement property in rst.RequestProperties)
                    {
                        property.WriteTo(writer);
                    }
                }

                rst.OnWriteCustomElements(writer);
                writer.WriteEndElement();
            }

            protected virtual void WriteTargets(RequestSecurityToken rst, XmlDictionaryWriter writer)
            {
            }

            // Note in Apr2004, internal & external references aren't supported - our strategy is to generate the external ref as the TokenReference.
            protected virtual void WriteReferences(RequestSecurityTokenResponse rstr, XmlDictionaryWriter writer)
            {
                if (rstr.RequestedUnattachedReference != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestedTokenReference, DriverDictionary.Namespace);
                    _standardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, rstr.RequestedUnattachedReference);
                    writer.WriteEndElement();
                }
            }

            protected virtual void WriteRequestedTokenClosed(RequestSecurityTokenResponse rstr, XmlDictionaryWriter writer)
            {
            }

            public override void WriteRequestSecurityTokenResponse(RequestSecurityTokenResponse rstr, XmlWriter xmlWriter)
            {
                if (rstr == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstr));
                }

                if (xmlWriter == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xmlWriter));
                }

                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter);
                if (rstr.IsReceiver)
                {
                    rstr.WriteTo(writer);
                    return;
                }
                writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestSecurityTokenResponse, DriverDictionary.Namespace);
                if (rstr.Context != null)
                {
                    writer.WriteAttributeString(DriverDictionary.Context, null, rstr.Context);
                }
                // define WSUtility at the top level to avoid multiple definitions below
                XmlHelper.AddNamespaceDeclaration(writer, UtilityStrings.Prefix, XD.UtilityDictionary.Namespace);
                rstr.OnWriteCustomAttributes(writer);

                if (rstr.TokenType != null)
                {
                    writer.WriteElementString(DriverDictionary.Prefix.Value, DriverDictionary.TokenType, DriverDictionary.Namespace, rstr.TokenType);
                }

                if (rstr.RequestedSecurityToken != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestedSecurityToken, DriverDictionary.Namespace);
                    _standardsManager.SecurityTokenSerializer.WriteToken(writer, rstr.RequestedSecurityToken);
                    writer.WriteEndElement();
                }

                if (rstr.AppliesTo != null)
                {
                    WriteAppliesTo(rstr.AppliesTo, rstr.AppliesToType, rstr.AppliesToSerializer, writer);
                }

                WriteReferences(rstr, writer);

                if (rstr.ComputeKey || rstr.RequestedProofToken != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestedProofToken, DriverDictionary.Namespace);
                    if (rstr.ComputeKey)
                    {
                        writer.WriteElementString(DriverDictionary.Prefix.Value, DriverDictionary.ComputedKey, DriverDictionary.Namespace, DriverDictionary.Psha1ComputedKeyUri.Value);
                    }
                    else
                    {
                        _standardsManager.SecurityTokenSerializer.WriteToken(writer, rstr.RequestedProofToken);
                    }
                    writer.WriteEndElement();
                }

                SecurityToken entropyToken = rstr.GetIssuerEntropy();
                if (entropyToken != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.Entropy, DriverDictionary.Namespace);
                    _standardsManager.SecurityTokenSerializer.WriteToken(writer, entropyToken);
                    writer.WriteEndElement();
                }

                // To write out the lifetime, the following algorithm is used
                //   1. If the lifetime is explicitly set, write it out.
                //   2. Else, if a token/tokenbuilder has been set, use the lifetime in that.
                //   3. Else do not serialize lifetime
                if (rstr.IsLifetimeSet || rstr.RequestedSecurityToken != null)
                {
                    DateTime effectiveTime = SecurityUtils.MinUtcDateTime;
                    DateTime expirationTime = SecurityUtils.MaxUtcDateTime;

                    if (rstr.IsLifetimeSet)
                    {
                        effectiveTime = rstr.ValidFrom.ToUniversalTime();
                        expirationTime = rstr.ValidTo.ToUniversalTime();
                    }
                    else if (rstr.RequestedSecurityToken != null)
                    {
                        effectiveTime = rstr.RequestedSecurityToken.ValidFrom.ToUniversalTime();
                        expirationTime = rstr.RequestedSecurityToken.ValidTo.ToUniversalTime();
                    }

                    // write out the lifetime
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.Lifetime, DriverDictionary.Namespace);
                    // write out Created
                    writer.WriteStartElement(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.CreatedElement, XD.UtilityDictionary.Namespace);
                    writer.WriteString(effectiveTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture.DateTimeFormat));
                    writer.WriteEndElement(); // wsu:Created
                    // write out Expires
                    writer.WriteStartElement(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.ExpiresElement, XD.UtilityDictionary.Namespace);
                    writer.WriteString(expirationTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture.DateTimeFormat));
                    writer.WriteEndElement(); // wsu:Expires
                    writer.WriteEndElement(); // wsse:Lifetime
                }

                byte[] authenticator = rstr.GetAuthenticator();
                if (authenticator != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.Authenticator, DriverDictionary.Namespace);
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.CombinedHash, DriverDictionary.Namespace);
                    writer.WriteBase64(authenticator, 0, authenticator.Length);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                if (rstr.KeySize > 0)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.KeySize, DriverDictionary.Namespace);
                    writer.WriteValue(rstr.KeySize);
                    writer.WriteEndElement();
                }

                WriteRequestedTokenClosed(rstr, writer);

                BinaryNegotiation negotiationData = rstr.GetBinaryNegotiation();
                if (negotiationData != null)
                {
                    WriteBinaryNegotiation(negotiationData, writer);
                }

                rstr.OnWriteCustomElements(writer);
                writer.WriteEndElement();
            }

            public override void WriteRequestSecurityTokenResponseCollection(RequestSecurityTokenResponseCollection rstrCollection, XmlWriter xmlWriter)
            {
                if (rstrCollection == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstrCollection));
                }

                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter);
                writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestSecurityTokenResponseCollection, DriverDictionary.Namespace);
                foreach (RequestSecurityTokenResponse rstr in rstrCollection.RstrCollection)
                {
                    rstr.WriteTo(writer);
                }
                writer.WriteEndElement();
            }

            protected void SetProtectionLevelForFederation(OperationDescriptionCollection operations)
            {
                foreach (OperationDescription operation in operations)
                {
                    foreach (MessageDescription message in operation.Messages)
                    {
                        if (message.Body.Parts.Count > 0)
                        {
                            foreach (MessagePartDescription part in message.Body.Parts)
                            {
                                part.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
                            }
                        }
                        if (OperationFormatter.IsValidReturnValue(message.Body.ReturnValue))
                        {
                            message.Body.ReturnValue.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
                        }
                    }
                }
            }

            public override bool TryParseKeySizeElement(XmlElement element, out int keySize)
            {
                if (element == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
                }

                if (element.LocalName == DriverDictionary.KeySize.Value
                    && element.NamespaceURI == DriverDictionary.Namespace.Value)
                {
                    keySize = int.Parse(XmlHelper.ReadTextElementAsTrimmedString(element), NumberFormatInfo.InvariantInfo);
                    return true;
                }

                keySize = 0;
                return false;
            }

            public override XmlElement CreateKeySizeElement(int keySize)
            {
                if (keySize < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(keySize), SRCommon.ValueMustBeNonNegative));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.KeySize.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(keySize.ToString(CultureInfo.InvariantCulture.NumberFormat)));
                return result;
            }

            public override XmlElement CreateKeyTypeElement(SecurityKeyType keyType)
            {
                if (keyType == SecurityKeyType.SymmetricKey)
                {
                    return CreateSymmetricKeyTypeElement();
                }
                else if (keyType == SecurityKeyType.AsymmetricKey)
                {
                    return CreatePublicKeyTypeElement();
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.UnableToCreateKeyTypeElementForUnknownKeyType, keyType.ToString())));
                }
            }

            public override bool TryParseKeyTypeElement(XmlElement element, out SecurityKeyType keyType)
            {
                if (element == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
                }

                if (TryParseSymmetricKeyElement(element))
                {
                    keyType = SecurityKeyType.SymmetricKey;
                    return true;
                }
                else if (TryParsePublicKeyElement(element))
                {
                    keyType = SecurityKeyType.AsymmetricKey;
                    return true;
                }

                keyType = SecurityKeyType.SymmetricKey;
                return false;
            }

            public bool TryParseSymmetricKeyElement(XmlElement element)
            {
                if (element == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
                }

                return element.LocalName == DriverDictionary.KeyType.Value
                    && element.NamespaceURI == DriverDictionary.Namespace.Value
                    && element.InnerText == DriverDictionary.SymmetricKeyType.Value;
            }

            private XmlElement CreateSymmetricKeyTypeElement()
            {
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.KeyType.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(DriverDictionary.SymmetricKeyType.Value));
                return result;
            }

            private bool TryParsePublicKeyElement(XmlElement element)
            {
                if (element == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
                }

                return element.LocalName == DriverDictionary.KeyType.Value
                    && element.NamespaceURI == DriverDictionary.Namespace.Value
                    && element.InnerText == DriverDictionary.PublicKeyType.Value;
            }

            private XmlElement CreatePublicKeyTypeElement()
            {
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.KeyType.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(DriverDictionary.PublicKeyType.Value));
                return result;
            }

            public override bool TryParseTokenTypeElement(XmlElement element, out string tokenType)
            {
                if (element == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
                }

                if (element.LocalName == DriverDictionary.TokenType.Value
                    && element.NamespaceURI == DriverDictionary.Namespace.Value)
                {
                    tokenType = element.InnerText;
                    return true;
                }

                tokenType = null;
                return false;
            }

            public override XmlElement CreateTokenTypeElement(string tokenTypeUri)
            {
                if (tokenTypeUri == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenTypeUri));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.TokenType.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(tokenTypeUri));
                return result;
            }

            public override XmlElement CreateUseKeyElement(SecurityKeyIdentifier keyIdentifier, SecurityStandardsManager standardsManager)
            {
                if (keyIdentifier == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));
                }
                if (standardsManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(standardsManager));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.UseKey.Value, DriverDictionary.Namespace.Value);
                MemoryStream stream = new MemoryStream();
                using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(new XmlTextWriter(stream, Encoding.UTF8)))
                {
                    standardsManager.SecurityTokenSerializer.WriteKeyIdentifier(writer, keyIdentifier);
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);
                    XmlNode skiNode;
                    using (XmlDictionaryReader reader = XmlDictionaryReader.CreateDictionaryReader(new XmlTextReader(stream) { DtdProcessing = DtdProcessing.Prohibit }))
                    {
                        reader.MoveToContent();
                        skiNode = doc.ReadNode(reader);
                    }
                    result.AppendChild(skiNode);
                }
                return result;
            }

            public override XmlElement CreateSignWithElement(string signatureAlgorithm)
            {
                if (signatureAlgorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(signatureAlgorithm));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.SignWith.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(signatureAlgorithm));
                return result;
            }

            internal override bool IsSignWithElement(XmlElement element, out string signatureAlgorithm)
            {
                return CheckElement(element, DriverDictionary.SignWith.Value, DriverDictionary.Namespace.Value, out signatureAlgorithm);
            }

            public override XmlElement CreateEncryptWithElement(string encryptionAlgorithm)
            {
                if (encryptionAlgorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encryptionAlgorithm));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.EncryptWith.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(encryptionAlgorithm));
                return result;
            }

            public override XmlElement CreateEncryptionAlgorithmElement(string encryptionAlgorithm)
            {
                if (encryptionAlgorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encryptionAlgorithm));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.EncryptionAlgorithm.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(encryptionAlgorithm));
                return result;
            }

            internal override bool IsEncryptWithElement(XmlElement element, out string encryptWithAlgorithm)
            {
                return CheckElement(element, DriverDictionary.EncryptWith.Value, DriverDictionary.Namespace.Value, out encryptWithAlgorithm);
            }

            internal override bool IsEncryptionAlgorithmElement(XmlElement element, out string encryptionAlgorithm)
            {
                return CheckElement(element, DriverDictionary.EncryptionAlgorithm.Value, DriverDictionary.Namespace.Value, out encryptionAlgorithm);
            }

            public override XmlElement CreateComputedKeyAlgorithmElement(string algorithm)
            {
                if (algorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(algorithm));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.ComputedKeyAlgorithm.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(algorithm));
                return result;
            }

            public override XmlElement CreateCanonicalizationAlgorithmElement(string algorithm)
            {
                if (algorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(algorithm));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.CanonicalizationAlgorithm.Value,
                    DriverDictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(algorithm));
                return result;
            }

            internal override bool IsCanonicalizationAlgorithmElement(XmlElement element, out string canonicalizationAlgorithm)
            {
                return CheckElement(element, DriverDictionary.CanonicalizationAlgorithm.Value, DriverDictionary.Namespace.Value, out canonicalizationAlgorithm);
            }

            public override bool TryParseRequiredClaimsElement(XmlElement element, out System.Collections.ObjectModel.Collection<XmlElement> requiredClaims)
            {
                if (element == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
                }

                if (element.LocalName == DriverDictionary.Claims.Value
                    && element.NamespaceURI == DriverDictionary.Namespace.Value)
                {
                    requiredClaims = new System.Collections.ObjectModel.Collection<XmlElement>();
                    foreach (XmlNode node in element.ChildNodes)
                    {
                        if (node is XmlElement)
                        {
                            requiredClaims.Add((XmlElement)node);
                        }
                    }

                    return true;
                }

                requiredClaims = null;
                return false;
            }

            public override XmlElement CreateRequiredClaimsElement(IEnumerable<XmlElement> claimsList)
            {
                if (claimsList == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(claimsList));
                }
                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DriverDictionary.Prefix.Value, DriverDictionary.Claims.Value,
                    DriverDictionary.Namespace.Value);
                foreach (XmlElement claimElement in claimsList)
                {
                    XmlElement element = (XmlElement)doc.ImportNode(claimElement, true);
                    result.AppendChild(element);
                }
                return result;
            }

            internal static void ValidateRequestedKeySize(int keySize, SecurityAlgorithmSuite algorithmSuite)
            {
                if ((keySize % 8 == 0) && algorithmSuite.IsSymmetricKeyLengthSupported(keySize))
                {
                    return;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidKeyLengthRequested, keySize)));
                }
            }

            private static void ValidateRequestorEntropy(SecurityToken entropy, SecurityKeyEntropyMode mode)
            {
                if ((mode == SecurityKeyEntropyMode.ClientEntropy || mode == SecurityKeyEntropyMode.CombinedEntropy)
                    && (entropy == null))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.EntropyModeRequiresRequestorEntropy, mode)));
                }
                if (mode == SecurityKeyEntropyMode.ServerEntropy && entropy != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.EntropyModeCannotHaveRequestorEntropy, mode)));
                }
            }

            internal static void ProcessRstAndIssueKey(RequestSecurityToken requestSecurityToken, SecurityTokenResolver resolver, SecurityKeyEntropyMode keyEntropyMode, SecurityAlgorithmSuite algorithmSuite, out int issuedKeySize, out byte[] issuerEntropy, out byte[] proofKey,
                out SecurityToken proofToken)
            {
                SecurityToken requestorEntropyToken = requestSecurityToken.GetRequestorEntropy(resolver);
                ValidateRequestorEntropy(requestorEntropyToken, keyEntropyMode);
                byte[] requestorEntropy;
                if (requestorEntropyToken != null)
                {
                    if (requestorEntropyToken is BinarySecretSecurityToken skToken)
                    {
                        requestorEntropy = skToken.GetKeyBytes();
                    }
                    else if (requestorEntropyToken is WrappedKeySecurityToken)
                    {
                        requestorEntropy = ((WrappedKeySecurityToken)requestorEntropyToken).GetWrappedKey();
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.TokenCannotCreateSymmetricCrypto, requestorEntropyToken)));
                    }
                }
                else
                {
                    requestorEntropy = null;
                }

                if (keyEntropyMode == SecurityKeyEntropyMode.ClientEntropy)
                {
                    if (requestorEntropy != null)
                    {
                        // validate that the entropy length matches the algorithm suite
                        ValidateRequestedKeySize(requestorEntropy.Length * 8, algorithmSuite);
                    }
                    proofKey = requestorEntropy;
                    issuerEntropy = null;
                    issuedKeySize = 0;
                    proofToken = null;
                }
                else
                {
                    if (requestSecurityToken.KeySize != 0)
                    {
                        ValidateRequestedKeySize(requestSecurityToken.KeySize, algorithmSuite);
                        issuedKeySize = requestSecurityToken.KeySize;
                    }
                    else
                    {
                        issuedKeySize = algorithmSuite.DefaultSymmetricKeyLength;
                    }
                    RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
                    if (keyEntropyMode == SecurityKeyEntropyMode.ServerEntropy)
                    {
                        proofKey = new byte[issuedKeySize / 8];
                        // proof key is completely issued by the server
                        random.GetNonZeroBytes(proofKey);
                        issuerEntropy = null;
                        proofToken = new BinarySecretSecurityToken(proofKey);
                    }
                    else
                    {
                        issuerEntropy = new byte[issuedKeySize / 8];
                        random.GetNonZeroBytes(issuerEntropy);
                        proofKey = RequestSecurityTokenResponse.ComputeCombinedKey(requestorEntropy, issuerEntropy, issuedKeySize);
                        proofToken = null;
                    }
                }
            }
        }

        protected static bool CheckElement(XmlElement element, string name, string ns, out string value)
        {
            value = null;
            if (element.LocalName != name || element.NamespaceURI != ns)
            {
                return false;
            }

            if (element.FirstChild is XmlText)
            {
                value = ((XmlText)element.FirstChild).Value;
                return true;
            }
            return false;
        }
    }
}
