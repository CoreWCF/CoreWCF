// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public sealed class FederatedMessageSecurityOverHttp
    {
        internal const bool DefaultNegotiateServiceCredential = true;
        internal const SecurityKeyType DefaultIssuedKeyType = SecurityKeyType.SymmetricKey;
        internal const bool DefaultEstablishSecurityContext = true;
        private SecurityAlgorithmSuite _algorithmSuite;
        private SecurityKeyType _issuedKeyType;

        public FederatedMessageSecurityOverHttp()
        {
            NegotiateServiceCredential = DefaultNegotiateServiceCredential;
            _algorithmSuite = SecurityAlgorithmSuite.Default;
            _issuedKeyType = DefaultIssuedKeyType;
            ClaimTypeRequirements = new Collection<ClaimTypeRequirement>();
            TokenRequestParameters = new Collection<XmlElement>();
            EstablishSecurityContext = DefaultEstablishSecurityContext;
        }

        public bool NegotiateServiceCredential { get; set; }

        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return _algorithmSuite; }
            set
            {
                _algorithmSuite = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public bool EstablishSecurityContext { get; set; }

        public EndpointAddress IssuerAddress { get; set; }

        public EndpointAddress IssuerMetadataAddress { get; set; }

        public Binding IssuerBinding { get; set; }

        public string IssuedTokenType { get; set; }

        public SecurityKeyType IssuedKeyType
        {
            get { return _issuedKeyType; }
            set
            {
                if (!SecurityKeyTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new System.ArgumentOutOfRangeException(nameof(value)));
                }
                _issuedKeyType = value;
            }
        }

        public Collection<ClaimTypeRequirement> ClaimTypeRequirements { get; }

        public Collection<XmlElement> TokenRequestParameters { get; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public SecurityBindingElement CreateSecurityBindingElement(bool isSecureTransportMode,
                                                                     bool isReliableSession,
                                                                     MessageSecurityVersion version)
        {
            if ((IssuedKeyType == SecurityKeyType.BearerKey) &&
               (version.TrustVersion == TrustVersion.WSTrustFeb2005))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BearerKeyIncompatibleWithWSFederationHttpBinding)));
            }

            if (isReliableSession && !EstablishSecurityContext)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationRequiredByReliableSession)));
            }

            SecurityBindingElement result;
            bool emitBspAttributes = true;
            IssuedSecurityTokenParameters issuedParameters = new IssuedSecurityTokenParameters(IssuedTokenType, IssuerAddress, IssuerBinding);
            issuedParameters.IssuerMetadataAddress = IssuerMetadataAddress;
            issuedParameters.KeyType = IssuedKeyType;
            if (IssuedKeyType == SecurityKeyType.SymmetricKey)
            {
                issuedParameters.KeySize = AlgorithmSuite.DefaultSymmetricKeyLength;
            }
            else
            {
                issuedParameters.KeySize = 0;
            }
            foreach (ClaimTypeRequirement c in ClaimTypeRequirements)
            {
                issuedParameters.ClaimTypeRequirements.Add(c);
            }
            foreach (XmlElement p in TokenRequestParameters)
            {
                issuedParameters.AdditionalRequestParameters.Add(p);
            }
            WSSecurityTokenSerializer versionSpecificSerializer = new WSSecurityTokenSerializer(version.SecurityVersion,
                                                                                                version.TrustVersion,
                                                                                                version.SecureConversationVersion,
                                                                                                emitBspAttributes,
                                                                                                null, null, null);
            SecurityStandardsManager versionSpecificStandardsManager = new SecurityStandardsManager(version, versionSpecificSerializer);
            issuedParameters.AddAlgorithmParameters(AlgorithmSuite, versionSpecificStandardsManager, _issuedKeyType);

            SecurityBindingElement issuedTokenSecurity;
            if (isSecureTransportMode)
            {
                issuedTokenSecurity = SecurityBindingElement.CreateIssuedTokenOverTransportBindingElement(issuedParameters);
            }
            else
            {
                if (NegotiateServiceCredential)
                {
                    // We should have passed 'true' as RequireCancelation to be consistent with other standard bindings.
                    // However, to limit the change for Orcas, we scope down to just newer version of WSSecurityPolicy.
                    issuedTokenSecurity = SecurityBindingElement.CreateIssuedTokenForSslBindingElement(issuedParameters, version.SecurityPolicyVersion != SecurityPolicyVersion.WSSecurityPolicy11);
                }
                else
                {
                    issuedTokenSecurity = SecurityBindingElement.CreateIssuedTokenForCertificateBindingElement(issuedParameters);
                }
            }

            issuedTokenSecurity.MessageSecurityVersion = version;
            issuedTokenSecurity.DefaultAlgorithmSuite = AlgorithmSuite;

            if (EstablishSecurityContext)
            {
                result = SecurityBindingElement.CreateSecureConversationBindingElement(issuedTokenSecurity, true);
            }
            else
            {
                result = issuedTokenSecurity;
            }

            result.MessageSecurityVersion = version;
            result.DefaultAlgorithmSuite = AlgorithmSuite;
            result.IncludeTimestamp = true;

            if (!isReliableSession)
            {
                result.LocalServiceSettings.ReconnectTransportOnFailure = false;
            }
            else
            {
                result.LocalServiceSettings.ReconnectTransportOnFailure = true;
            }

            if (EstablishSecurityContext)
            {
                // issue the transition SCT for a short duration only
                issuedTokenSecurity.LocalServiceSettings.IssuedCookieLifetime = SpnegoTokenAuthenticator.s_defaultServerIssuedTransitionTokenLifetime;
            }

            return result;
        }

    }
}
