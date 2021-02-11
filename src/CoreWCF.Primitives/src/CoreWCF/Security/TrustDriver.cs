// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Xml;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    internal abstract class TrustDriver
    {
        // issued tokens control        
        public virtual bool IsIssuedTokensSupported => false;

        // issued tokens feature        
        public virtual string IssuedTokensHeaderName =>
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.TrustDriverVersionDoesNotSupportIssuedTokens)));

        // issued tokens feature        
        public virtual string IssuedTokensHeaderNamespace =>
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.TrustDriverVersionDoesNotSupportIssuedTokens)));

        // session control
        public virtual bool IsSessionSupported => false;

        public abstract XmlDictionaryString RequestSecurityTokenAction { get; }

        public abstract XmlDictionaryString RequestSecurityTokenResponseAction { get; }

        public abstract XmlDictionaryString RequestSecurityTokenResponseFinalAction { get; }

        // session feature
        public virtual string RequestTypeClose =>
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.TrustDriverVersionDoesNotSupportSession)));

        public abstract string RequestTypeIssue { get; }

        // session feature
        public virtual string RequestTypeRenew =>
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.TrustDriverVersionDoesNotSupportSession)));

        public abstract string ComputedKeyAlgorithm { get; }

        public abstract SecurityStandardsManager StandardsManager { get; }

        public abstract XmlDictionaryString Namespace { get; }

        // RST specific method
        public abstract RequestSecurityToken CreateRequestSecurityToken(XmlReader reader);

        // RSTR specific method
        public abstract RequestSecurityTokenResponse CreateRequestSecurityTokenResponse(XmlReader reader);

        // RSTRC specific method
        public abstract RequestSecurityTokenResponseCollection CreateRequestSecurityTokenResponseCollection(XmlReader xmlReader);

        public abstract bool IsAtRequestSecurityTokenResponse(XmlReader reader);

        public abstract bool IsAtRequestSecurityTokenResponseCollection(XmlReader reader);

        public abstract bool IsRequestedSecurityTokenElement(string name, string nameSpace);

        public abstract bool IsRequestedProofTokenElement(string name, string nameSpace);

        public abstract T GetAppliesTo<T>(RequestSecurityToken rst, XmlObjectSerializer serializer);

        public abstract T GetAppliesTo<T>(RequestSecurityTokenResponse rstr, XmlObjectSerializer serializer);

        public abstract void GetAppliesToQName(RequestSecurityToken rst, out string localName, out string namespaceUri);

        public abstract void GetAppliesToQName(RequestSecurityTokenResponse rstr, out string localName, out string namespaceUri);

        public abstract bool IsAppliesTo(string localName, string namespaceUri);

        // RSTR specific method
        public abstract byte[] GetAuthenticator(RequestSecurityTokenResponse rstr);

        // RST specific method
        public abstract BinaryNegotiation GetBinaryNegotiation(RequestSecurityToken rst);

        // RSTR specific method
        public abstract BinaryNegotiation GetBinaryNegotiation(RequestSecurityTokenResponse rstr);

        // RST specific method
        public abstract SecurityToken GetEntropy(RequestSecurityToken rst, SecurityTokenResolver resolver);

        // RSTR specific method
        public abstract SecurityToken GetEntropy(RequestSecurityTokenResponse rstr, SecurityTokenResolver resolver);

        // RSTR specific method
        public abstract GenericXmlSecurityToken GetIssuedToken(RequestSecurityTokenResponse rstr, SecurityTokenResolver resolver, IList<SecurityTokenAuthenticator> allowedAuthenticators, SecurityKeyEntropyMode keyEntropyMode, byte[] requestorEntropy,
            string expectedTokenType, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, int defaultKeySize, bool isBearerKeyType);

        public abstract GenericXmlSecurityToken GetIssuedToken(RequestSecurityTokenResponse rstr, string expectedTokenType, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, RSA clientKey);

        public abstract void OnRSTRorRSTRCMissingException();

        // RST specific method
        public abstract void WriteRequestSecurityToken(RequestSecurityToken rst, XmlWriter w);

        // RSTR specific method
        public abstract void WriteRequestSecurityTokenResponse(RequestSecurityTokenResponse rstr, XmlWriter w);

        // RSTR Collection method
        public abstract void WriteRequestSecurityTokenResponseCollection(RequestSecurityTokenResponseCollection rstrCollection, XmlWriter writer);

        // Federation proxy creation
        //  public abstract IChannelFactory<IRequestChannel> CreateFederationProxy(EndpointAddress address, Binding binding, KeyedByTypeCollection<IEndpointBehavior> channelBehaviors);
        public abstract XmlElement CreateKeySizeElement(int keySize);
        public abstract XmlElement CreateKeyTypeElement(SecurityKeyType keyType);
        public abstract XmlElement CreateTokenTypeElement(string tokenTypeUri);
        public abstract XmlElement CreateRequiredClaimsElement(IEnumerable<XmlElement> claimsList);
        public abstract XmlElement CreateUseKeyElement(SecurityKeyIdentifier keyIdentifier, SecurityStandardsManager standardsManager);
        public abstract XmlElement CreateSignWithElement(string signatureAlgorithm);
        public abstract XmlElement CreateEncryptWithElement(string encryptionAlgorithm);
        public abstract XmlElement CreateEncryptionAlgorithmElement(string encryptionAlgorithm);
        public abstract XmlElement CreateCanonicalizationAlgorithmElement(string canonicalicationAlgorithm);
        public abstract XmlElement CreateComputedKeyAlgorithmElement(string computedKeyAlgorithm);
        public abstract Collection<XmlElement> ProcessUnknownRequestParameters(Collection<XmlElement> unknownRequestParameters, Collection<XmlElement> originalRequestParameters);
        public abstract bool TryParseKeySizeElement(XmlElement element, out int keySize);
        public abstract bool TryParseKeyTypeElement(XmlElement element, out SecurityKeyType keyType);
        public abstract bool TryParseTokenTypeElement(XmlElement element, out string tokenType);
        public abstract bool TryParseRequiredClaimsElement(XmlElement element, out Collection<XmlElement> requiredClaims);
        // helper methods for the parsing standard binding elements
        internal virtual bool IsSignWithElement(XmlElement element, out string signatureAlgorithm) { signatureAlgorithm = null; return false; }
        internal virtual bool IsEncryptWithElement(XmlElement element, out string encryptWithAlgorithm) { encryptWithAlgorithm = null; return false; }
        internal virtual bool IsEncryptionAlgorithmElement(XmlElement element, out string encryptionAlgorithm) { encryptionAlgorithm = null; return false; }
        internal virtual bool IsCanonicalizationAlgorithmElement(XmlElement element, out string canonicalizationAlgorithm) { canonicalizationAlgorithm = null; return false; }
        internal virtual bool IsKeyWrapAlgorithmElement(XmlElement element, out string keyWrapAlgorithm) { keyWrapAlgorithm = null; return false; }
    }
}
