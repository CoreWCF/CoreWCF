// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class ServiceCredentialsSecurityTokenManager : SecurityTokenManager, IEndpointIdentityProvider
    {
        public ServiceCredentialsSecurityTokenManager(ServiceCredentials parent)
        {
            ServiceCredentials = parent ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
        }

        public ServiceCredentials ServiceCredentials { get; }

        internal override SecurityTokenSerializer CreateSecurityTokenSerializer(SecurityTokenVersion version)
        {
            if (version == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
            }
            if (version is MessageSecurityTokenVersion wsVersion)
            {
                SamlSerializer samlSerializer = null;
                //TODO this will be implemented when we add WS-Federation support
                //if (parent.IssuedTokenAuthentication != null)
                //    samlSerializer = parent.IssuedTokenAuthentication.SamlSerializer;
                //else
                //    samlSerializer = new SamlSerializer();

                return new WSSecurityTokenSerializer(wsVersion.SecurityVersion, wsVersion.TrustVersion, wsVersion.SecureConversationVersion, wsVersion.EmitBspRequiredAttributes, samlSerializer, ServiceCredentials.SecureConversationAuthentication.SecurityStateEncoder, ServiceCredentials.SecureConversationAuthentication.SecurityContextClaimTypes);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SecurityTokenManagerCannotCreateSerializerForVersion, version)));
            }
        }

        protected SecurityTokenAuthenticator CreateSecureConversationTokenAuthenticator(RecipientServiceModelSecurityTokenRequirement recipientRequirement, bool preserveBootstrapTokens, out SecurityTokenResolver sctResolver)
        {
            SecurityBindingElement securityBindingElement = recipientRequirement.SecurityBindingElement;
            if (securityBindingElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.TokenAuthenticatorRequiresSecurityBindingElement, (object)recipientRequirement));
            }

            bool flag = !recipientRequirement.SupportSecurityContextCancellation;
            LocalServiceSecuritySettings localServiceSettings = securityBindingElement.LocalServiceSettings;
            IMessageFilterTable<EndpointAddress> propertyOrDefault = recipientRequirement.GetPropertyOrDefault<IMessageFilterTable<EndpointAddress>>(ServiceModelSecurityTokenRequirement.EndpointFilterTableProperty, (IMessageFilterTable<EndpointAddress>)null);
            if (!flag)
            {
                sctResolver = (SecurityTokenResolver)new SecurityContextSecurityTokenResolver(int.MaxValue, false);
                return (SecurityTokenAuthenticator)new SecuritySessionSecurityTokenAuthenticator()
                {
                    BootstrapSecurityBindingElement = SecurityUtils.GetIssuerSecurityBindingElement((ServiceModelSecurityTokenRequirement)recipientRequirement),
                    IssuedSecurityTokenParameters = recipientRequirement.GetProperty<SecurityTokenParameters>(ServiceModelSecurityTokenRequirement.IssuedSecurityTokenParametersProperty),
                    IssuedTokenCache = (ISecurityContextSecurityTokenCache)sctResolver,
                    IssuerBindingContext = recipientRequirement.GetProperty<BindingContext>(ServiceModelSecurityTokenRequirement.IssuerBindingContextProperty),
                    KeyEntropyMode = securityBindingElement.KeyEntropyMode,
                    ListenUri = recipientRequirement.ListenUri,
                    SecurityAlgorithmSuite = recipientRequirement.SecurityAlgorithmSuite,
                    SessionTokenLifetime = TimeSpan.MaxValue,
                    KeyRenewalInterval = securityBindingElement.LocalServiceSettings.SessionKeyRenewalInterval,
                    StandardsManager = SecurityUtils.CreateSecurityStandardsManager((SecurityTokenRequirement)recipientRequirement, (SecurityTokenManager)this),
                    EndpointFilterTable = propertyOrDefault,
                    MaximumConcurrentNegotiations = localServiceSettings.MaxStatefulNegotiations,
                    NegotiationTimeout = localServiceSettings.NegotiationTimeout,
                    PreserveBootstrapTokens = preserveBootstrapTokens
                };
            }
            throw new NotImplementedException();
            /* TODO later
            sctResolver = (SecurityTokenResolver)new SecurityContextSecurityTokenResolver(localServiceSettings.MaxCachedCookies, true, localServiceSettings.MaxClockSkew);
            AcceleratedTokenAuthenticator tokenAuthenticator = new AcceleratedTokenAuthenticator();
            tokenAuthenticator.BootstrapSecurityBindingElement = SecurityUtils.GetIssuerSecurityBindingElement((ServiceModelSecurityTokenRequirement)recipientRequirement);
            tokenAuthenticator.KeyEntropyMode = securityBindingElement.KeyEntropyMode;
            tokenAuthenticator.EncryptStateInServiceToken = true;
            tokenAuthenticator.IssuedSecurityTokenParameters = recipientRequirement.GetProperty<SecurityTokenParameters>(ServiceModelSecurityTokenRequirement.IssuedSecurityTokenParametersProperty);
            tokenAuthenticator.IssuedTokenCache = (ISecurityContextSecurityTokenCache)sctResolver;
            tokenAuthenticator.IssuerBindingContext = recipientRequirement.GetProperty<BindingContext>(ServiceModelSecurityTokenRequirement.IssuerBindingContextProperty);
            tokenAuthenticator.ListenUri = recipientRequirement.ListenUri;
            tokenAuthenticator.SecurityAlgorithmSuite = recipientRequirement.SecurityAlgorithmSuite;
            tokenAuthenticator.StandardsManager = SecurityUtils.CreateSecurityStandardsManager((SecurityTokenRequirement)recipientRequirement, (SecurityTokenManager)this);
            tokenAuthenticator.SecurityStateEncoder = this.parent.SecureConversationAuthentication.SecurityStateEncoder;
            tokenAuthenticator.KnownTypes = (IList<System.Type>)this.parent.SecureConversationAuthentication.SecurityContextClaimTypes;
            tokenAuthenticator.PreserveBootstrapTokens = preserveBootstrapTokens;
            tokenAuthenticator.MaximumCachedNegotiationState = localServiceSettings.MaxStatefulNegotiations;
            tokenAuthenticator.NegotiationTimeout = localServiceSettings.NegotiationTimeout;
            tokenAuthenticator.ServiceTokenLifetime = localServiceSettings.IssuedCookieLifetime;
            tokenAuthenticator.MaximumConcurrentNegotiations = localServiceSettings.MaxStatefulNegotiations;
            tokenAuthenticator.AuditLogLocation = recipientRequirement.AuditLogLocation;
            tokenAuthenticator.SuppressAuditFailure = recipientRequirement.SuppressAuditFailure;
            tokenAuthenticator.MessageAuthenticationAuditLevel = recipientRequirement.MessageAuthenticationAuditLevel;
            tokenAuthenticator.EndpointFilterTable = propertyOrDefault;
            return (SecurityTokenAuthenticator)tokenAuthenticator;*/

        }

        private SecurityTokenAuthenticator CreateSpnegoSecurityTokenAuthenticator(RecipientServiceModelSecurityTokenRequirement recipientRequirement, out SecurityTokenResolver sctResolver)
        {
            throw new PlatformNotSupportedException("SpnegoSecurityTokenAuthenticator");
            //SecurityBindingElement securityBindingElement = recipientRequirement.SecurityBindingElement;
            //if (securityBindingElement == null)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.TokenAuthenticatorRequiresSecurityBindingElement, recipientRequirement));
            //}
            //bool isCookieMode = !recipientRequirement.SupportSecurityContextCancellation;
            //LocalServiceSecuritySettings localServiceSettings = securityBindingElement.LocalServiceSettings;
            //sctResolver = new SecurityContextSecurityTokenResolver(localServiceSettings.MaxCachedCookies, true);
            //ExtendedProtectionPolicy extendedProtectionPolicy = null;
            //recipientRequirement.TryGetProperty<ExtendedProtectionPolicy>(ServiceModelSecurityTokenRequirement.ExtendedProtectionPolicy, out extendedProtectionPolicy);

            //SpnegoTokenAuthenticator authenticator = new SpnegoTokenAuthenticator();
            //authenticator.ExtendedProtectionPolicy = extendedProtectionPolicy;
            //authenticator.AllowUnauthenticatedCallers = parent.WindowsAuthentication.AllowAnonymousLogons;
            //authenticator.ExtractGroupsForWindowsAccounts = parent.WindowsAuthentication.IncludeWindowsGroups;
            //authenticator.IsClientAnonymous = false;
            //authenticator.EncryptStateInServiceToken = isCookieMode;
            //authenticator.IssuedSecurityTokenParameters = recipientRequirement.GetProperty<SecurityTokenParameters>(ServiceModelSecurityTokenRequirement.IssuedSecurityTokenParametersProperty);
            //authenticator.IssuedTokenCache = (ISecurityContextSecurityTokenCache)sctResolver;
            //authenticator.IssuerBindingContext = recipientRequirement.GetProperty<BindingContext>(ServiceModelSecurityTokenRequirement.IssuerBindingContextProperty);
            //authenticator.ListenUri = recipientRequirement.ListenUri;
            //authenticator.SecurityAlgorithmSuite = recipientRequirement.SecurityAlgorithmSuite;
            //authenticator.StandardsManager = SecurityUtils.CreateSecurityStandardsManager(recipientRequirement, this);
            //authenticator.SecurityStateEncoder = parent.SecureConversationAuthentication.SecurityStateEncoder;
            //authenticator.KnownTypes = parent.SecureConversationAuthentication.SecurityContextClaimTypes;
            //// if the SPNEGO is being done in mixed-mode, the nego blobs are from an anonymous client and so there size bound needs to be enforced.
            //if (securityBindingElement is TransportSecurityBindingElement)
            //{
            //    authenticator.MaxMessageSize = SecurityUtils.GetMaxNegotiationBufferSize(authenticator.IssuerBindingContext);
            //}

            //// local security quotas
            //authenticator.MaximumCachedNegotiationState = localServiceSettings.MaxStatefulNegotiations;
            //authenticator.NegotiationTimeout = localServiceSettings.NegotiationTimeout;
            //authenticator.ServiceTokenLifetime = localServiceSettings.IssuedCookieLifetime;
            //authenticator.MaximumConcurrentNegotiations = localServiceSettings.MaxStatefulNegotiations;

            //// audit settings
            //authenticator.AuditLogLocation = recipientRequirement.AuditLogLocation;
            //authenticator.SuppressAuditFailure = recipientRequirement.SuppressAuditFailure;
            //authenticator.MessageAuthenticationAuditLevel = recipientRequirement.MessageAuthenticationAuditLevel;
            //return authenticator;
        }

        private SecurityTokenAuthenticator CreateTlsnegoClientX509TokenAuthenticator(RecipientServiceModelSecurityTokenRequirement recipientRequirement)
        {
            throw new PlatformNotSupportedException("TlsnegoClientX509Token");
            //RecipientServiceModelSecurityTokenRequirement clientX509Requirement = new RecipientServiceModelSecurityTokenRequirement();
            //clientX509Requirement.TokenType = SecurityTokenTypes.X509Certificate;
            //clientX509Requirement.KeyUsage = SecurityKeyUsage.Signature;
            //clientX509Requirement.ListenUri = recipientRequirement.ListenUri;
            //clientX509Requirement.KeyType = SecurityKeyType.AsymmetricKey;
            //clientX509Requirement.SecurityBindingElement = recipientRequirement.SecurityBindingElement;
            //SecurityTokenResolver dummy;
            //return this.CreateSecurityTokenAuthenticator(clientX509Requirement, out dummy);
        }

        private SecurityTokenProvider CreateTlsnegoServerX509TokenProvider(RecipientServiceModelSecurityTokenRequirement recipientRequirement)
        {
            throw new PlatformNotSupportedException("TlsnegoServerX509Token");
            //RecipientServiceModelSecurityTokenRequirement serverX509Requirement = new RecipientServiceModelSecurityTokenRequirement();
            //serverX509Requirement.TokenType = SecurityTokenTypes.X509Certificate;
            //serverX509Requirement.KeyUsage = SecurityKeyUsage.Exchange;
            //serverX509Requirement.ListenUri = recipientRequirement.ListenUri;
            //serverX509Requirement.KeyType = SecurityKeyType.AsymmetricKey;
            //serverX509Requirement.SecurityBindingElement = recipientRequirement.SecurityBindingElement;
            //return this.CreateSecurityTokenProvider(serverX509Requirement);
        }

        private SecurityTokenAuthenticator CreateTlsnegoSecurityTokenAuthenticator(RecipientServiceModelSecurityTokenRequirement recipientRequirement, bool requireClientCertificate, out SecurityTokenResolver sctResolver)
        {
            throw new PlatformNotSupportedException("TlsnegoSecurityToken");
            //SecurityBindingElement securityBindingElement = recipientRequirement.SecurityBindingElement;
            //if (securityBindingElement == null)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.TokenAuthenticatorRequiresSecurityBindingElement, recipientRequirement));
            //}
            //bool isCookieMode = !recipientRequirement.SupportSecurityContextCancellation;
            //LocalServiceSecuritySettings localServiceSettings = securityBindingElement.LocalServiceSettings;
            //sctResolver = new SecurityContextSecurityTokenResolver(localServiceSettings.MaxCachedCookies, true);

            //TlsnegoTokenAuthenticator authenticator = new TlsnegoTokenAuthenticator();
            //authenticator.IsClientAnonymous = !requireClientCertificate;
            //if (requireClientCertificate)
            //{
            //    authenticator.ClientTokenAuthenticator = this.CreateTlsnegoClientX509TokenAuthenticator(recipientRequirement);
            //    authenticator.MapCertificateToWindowsAccount = this.ServiceCredentials.ClientCertificate.Authentication.MapClientCertificateToWindowsAccount;
            //}
            //authenticator.EncryptStateInServiceToken = isCookieMode;
            //authenticator.IssuedSecurityTokenParameters = recipientRequirement.GetProperty<SecurityTokenParameters>(ServiceModelSecurityTokenRequirement.IssuedSecurityTokenParametersProperty);
            //authenticator.IssuedTokenCache = (ISecurityContextSecurityTokenCache)sctResolver;
            //authenticator.IssuerBindingContext = recipientRequirement.GetProperty<BindingContext>(ServiceModelSecurityTokenRequirement.IssuerBindingContextProperty);
            //authenticator.ListenUri = recipientRequirement.ListenUri;
            //authenticator.SecurityAlgorithmSuite = recipientRequirement.SecurityAlgorithmSuite;
            //authenticator.StandardsManager = SecurityUtils.CreateSecurityStandardsManager(recipientRequirement, this);
            //authenticator.SecurityStateEncoder = parent.SecureConversationAuthentication.SecurityStateEncoder;
            //authenticator.KnownTypes = parent.SecureConversationAuthentication.SecurityContextClaimTypes;
            //authenticator.ServerTokenProvider = CreateTlsnegoServerX509TokenProvider(recipientRequirement);
            //// local security quotas
            //authenticator.MaximumCachedNegotiationState = localServiceSettings.MaxStatefulNegotiations;
            //authenticator.NegotiationTimeout = localServiceSettings.NegotiationTimeout;
            //authenticator.ServiceTokenLifetime = localServiceSettings.IssuedCookieLifetime;
            //authenticator.MaximumConcurrentNegotiations = localServiceSettings.MaxStatefulNegotiations;
            //// if the TLSNEGO is being done in mixed-mode, the nego blobs are from an anonymous client and so there size bound needs to be enforced.
            //if (securityBindingElement is TransportSecurityBindingElement)
            //{
            //    authenticator.MaxMessageSize = SecurityUtils.GetMaxNegotiationBufferSize(authenticator.IssuerBindingContext);
            //}
            //// audit settings
            //authenticator.AuditLogLocation = recipientRequirement.AuditLogLocation;
            //authenticator.SuppressAuditFailure = recipientRequirement.SuppressAuditFailure;
            //authenticator.MessageAuthenticationAuditLevel = recipientRequirement.MessageAuthenticationAuditLevel;
            //return authenticator;
        }

        private X509SecurityTokenAuthenticator CreateClientX509TokenAuthenticator()
        {
            X509ClientCertificateAuthentication authentication = ServiceCredentials.ClientCertificate.Authentication;
            return new X509SecurityTokenAuthenticator(authentication.GetCertificateValidator(), authentication.MapClientCertificateToWindowsAccount, authentication.IncludeWindowsGroups);
        }

        //SamlSecurityTokenAuthenticator CreateSamlTokenAuthenticator(RecipientServiceModelSecurityTokenRequirement recipientRequirement, out SecurityTokenResolver outOfBandTokenResolver)
        //{
        //    if (recipientRequirement == null)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(recipientRequirement));

        //    Collection<SecurityToken> outOfBandTokens = new Collection<SecurityToken>();
        //    if (parent.ServiceCertificate.Certificate != null)
        //    {
        //        outOfBandTokens.Add(new X509SecurityToken(parent.ServiceCertificate.Certificate));
        //    }
        //    List<SecurityTokenAuthenticator> supportingAuthenticators = new List<SecurityTokenAuthenticator>();
        //    if ((parent.IssuedTokenAuthentication.KnownCertificates != null) && (parent.IssuedTokenAuthentication.KnownCertificates.Count > 0))
        //    {
        //        for (int i = 0; i < parent.IssuedTokenAuthentication.KnownCertificates.Count; ++i)
        //        {
        //            outOfBandTokens.Add(new X509SecurityToken(parent.IssuedTokenAuthentication.KnownCertificates[i]));
        //        }
        //    }

        //    X509CertificateValidator validator = parent.IssuedTokenAuthentication.GetCertificateValidator();
        //    supportingAuthenticators.Add(new X509SecurityTokenAuthenticator(validator));

        //    if (parent.IssuedTokenAuthentication.AllowUntrustedRsaIssuers)
        //    {
        //        supportingAuthenticators.Add(new RsaSecurityTokenAuthenticator());
        //    }

        //    outOfBandTokenResolver = (outOfBandTokens.Count > 0) ? SecurityTokenResolver.CreateDefaultSecurityTokenResolver(new ReadOnlyCollection<SecurityToken>(outOfBandTokens), false) : null;

        //    SamlSecurityTokenAuthenticator ssta;

        //    if ((recipientRequirement.SecurityBindingElement == null) || (recipientRequirement.SecurityBindingElement.LocalServiceSettings == null))
        //    {
        //        ssta = new SamlSecurityTokenAuthenticator(supportingAuthenticators);
        //    }
        //    else
        //    {
        //        ssta = new SamlSecurityTokenAuthenticator(supportingAuthenticators, recipientRequirement.SecurityBindingElement.LocalServiceSettings.MaxClockSkew);
        //    }

        //    // set audience uri restrictions
        //    ssta.AudienceUriMode = parent.IssuedTokenAuthentication.AudienceUriMode;
        //    IList<string> allowedAudienceUris = ssta.AllowedAudienceUris;
        //    if (parent.IssuedTokenAuthentication.AllowedAudienceUris != null)
        //    {
        //        for (int i = 0; i < parent.IssuedTokenAuthentication.AllowedAudienceUris.Count; i++)
        //            allowedAudienceUris.Add(parent.IssuedTokenAuthentication.AllowedAudienceUris[i]);
        //    }

        //    if (recipientRequirement.ListenUri != null)
        //    {
        //        allowedAudienceUris.Add(recipientRequirement.ListenUri.AbsoluteUri);
        //    }

        //    return ssta;
        //}

        private X509SecurityTokenProvider CreateServerX509TokenProvider()
        {
            if (ServiceCredentials.ServiceCertificate.Certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ServiceCertificateNotProvidedOnServiceCredentials));
            }
            SecurityUtils.EnsureCertificateCanDoKeyExchange(ServiceCredentials.ServiceCertificate.Certificate);
            return new ServiceX509SecurityTokenProvider(ServiceCredentials.ServiceCertificate.Certificate);
        }

        protected bool IsIssuedSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            return (requirement != null && requirement.Properties.ContainsKey(ServiceModelSecurityTokenRequirement.IssuerAddressProperty));
        }

        public override SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver)
        {
            if (tokenRequirement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenRequirement));
            }
            string tokenType = tokenRequirement.TokenType;
            outOfBandTokenResolver = null;
            SecurityTokenAuthenticator result = null;
            if (tokenRequirement is InitiatorServiceModelSecurityTokenRequirement)
            {
                // this is the uncorrelated duplex case in which the server is asking for
                // an authenticator to validate its provisioned client certificate
                if (tokenType == SecurityTokenTypes.X509Certificate && tokenRequirement.KeyUsage == SecurityKeyUsage.Exchange)
                {
                    return new X509SecurityTokenAuthenticator(X509CertificateValidator.None, false);
                }
            }

            if (!(tokenRequirement is RecipientServiceModelSecurityTokenRequirement recipientRequirement))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SecurityTokenManagerCannotCreateAuthenticatorForRequirement, tokenRequirement)));
            }
            if (tokenType == SecurityTokenTypes.X509Certificate)
            {
                result = CreateClientX509TokenAuthenticator();
            }
            else if (tokenType == SecurityTokenTypes.Kerberos)
            {
                throw new PlatformNotSupportedException("KerberosSecurityTokenAuthenticator");
                //result = new KerberosSecurityTokenAuthenticatorWrapper(
                //    new KerberosSecurityTokenAuthenticator(parent.WindowsAuthentication.IncludeWindowsGroups));
            }
            else if (tokenType == SecurityTokenTypes.UserName)
            {
                if (ServiceCredentials.UserNameAuthentication.UserNamePasswordValidationMode == UserNamePasswordValidationMode.Windows)
                {
                    throw new PlatformNotSupportedException("UserNamePasswordValidationMode.Windows");
                    //if (parent.UserNameAuthentication.CacheLogonTokens)
                    //{
                    //    result = new WindowsUserNameCachingSecurityTokenAuthenticator(parent.UserNameAuthentication.IncludeWindowsGroups,
                    //        parent.UserNameAuthentication.MaxCachedLogonTokens, parent.UserNameAuthentication.CachedLogonTokenLifetime);
                    //}
                    //else
                    //{
                    //    result = new WindowsUserNameSecurityTokenAuthenticator(parent.UserNameAuthentication.IncludeWindowsGroups);
                    //}
                }
                else
                {
                    result = new CustomUserNameSecurityTokenAuthenticator(ServiceCredentials.UserNameAuthentication.GetUserNamePasswordValidator());
                }
            }
            else if (tokenType == SecurityTokenTypes.Rsa)
            {
                result = new RsaSecurityTokenAuthenticator();
            }
            else if (tokenType == ServiceModelSecurityTokenTypes.AnonymousSslnego)
            {
                result = CreateTlsnegoSecurityTokenAuthenticator(recipientRequirement, false, out outOfBandTokenResolver);
            }
            else if (tokenType == ServiceModelSecurityTokenTypes.MutualSslnego)
            {
                result = CreateTlsnegoSecurityTokenAuthenticator(recipientRequirement, true, out outOfBandTokenResolver);
            }
            else if (tokenType == ServiceModelSecurityTokenTypes.Spnego)
            {
                result = CreateSpnegoSecurityTokenAuthenticator(recipientRequirement, out outOfBandTokenResolver);
            }
            else if (tokenType == ServiceModelSecurityTokenTypes.SecureConversation)
            {
                result = CreateSecureConversationTokenAuthenticator(recipientRequirement, false, out outOfBandTokenResolver);
            }
            else if ((tokenType == SecurityTokenTypes.Saml)
                || (tokenType == SecurityXXX2005Strings.SamlTokenType)
                || (tokenType == SecurityJan2004Strings.SamlUri)
                || (tokenType == null && IsIssuedSecurityTokenRequirement(recipientRequirement)))
            {
                throw new PlatformNotSupportedException("SamlToken");
                //result = CreateSamlTokenAuthenticator(recipientRequirement, out outOfBandTokenResolver);
            }

            if (result == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SecurityTokenManagerCannotCreateAuthenticatorForRequirement, tokenRequirement)));
            }

            return result;
        }

        private SecurityTokenProvider CreateLocalSecurityTokenProvider(RecipientServiceModelSecurityTokenRequirement recipientRequirement)
        {
            string tokenType = recipientRequirement.TokenType;
            SecurityTokenProvider result = null;
            if (tokenType == SecurityTokenTypes.X509Certificate)
            {
                result = CreateServerX509TokenProvider();
            }
            else if (tokenType == ServiceModelSecurityTokenTypes.SspiCredential)
            {
                // if Transport Security, AuthenticationSchemes.Basic will look at parent.UserNameAuthentication settings.
                bool authenticationSchemeIdentified = recipientRequirement.TryGetProperty<AuthenticationSchemes>(ServiceModelSecurityTokenRequirement.HttpAuthenticationSchemeProperty, out AuthenticationSchemes authenticationScheme);
                if (authenticationSchemeIdentified &&
                    authenticationScheme.IsSet(AuthenticationSchemes.Basic) &&
                    authenticationScheme.IsNotSet(AuthenticationSchemes.Digest | AuthenticationSchemes.Ntlm | AuthenticationSchemes.Negotiate))
                {
                    // create security token provider even when basic and Anonymous are enabled.
                    result = new SspiSecurityTokenProvider(null, ServiceCredentials.UserNameAuthentication.IncludeWindowsGroups, false);
                }
                else
                {
                    if (authenticationSchemeIdentified &&
                       authenticationScheme.IsSet(AuthenticationSchemes.Basic) &&
                       ServiceCredentials.WindowsAuthentication.IncludeWindowsGroups != ServiceCredentials.UserNameAuthentication.IncludeWindowsGroups)
                    {
                        // Ensure there are no inconsistencies when Basic and (Digest and/or Ntlm and/or Negotiate) are both enabled
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SecurityTokenProviderIncludeWindowsGroupsInconsistent,
                            (AuthenticationSchemes)authenticationScheme - AuthenticationSchemes.Basic,
                            ServiceCredentials.UserNameAuthentication.IncludeWindowsGroups,
                            ServiceCredentials.WindowsAuthentication.IncludeWindowsGroups)));
                    }

                    result = new SspiSecurityTokenProvider(null, ServiceCredentials.WindowsAuthentication.IncludeWindowsGroups, ServiceCredentials.WindowsAuthentication.AllowAnonymousLogons);
                }
            }
            return result;
        }

        private SecurityTokenProvider CreateUncorrelatedDuplexSecurityTokenProvider(InitiatorServiceModelSecurityTokenRequirement initiatorRequirement)
        {
            string tokenType = initiatorRequirement.TokenType;
            SecurityTokenProvider result = null;
            if (tokenType == SecurityTokenTypes.X509Certificate)
            {
                SecurityKeyUsage keyUsage = initiatorRequirement.KeyUsage;
                if (keyUsage == SecurityKeyUsage.Exchange)
                {
                    if (ServiceCredentials.ClientCertificate.Certificate == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ClientCertificateNotProvidedOnServiceCredentials));
                    }

                    result = new X509SecurityTokenProvider(ServiceCredentials.ClientCertificate.Certificate);
                }
                else
                {
                    // this is a request for the server's own cert for signing
                    result = CreateServerX509TokenProvider();
                }
            }
            return result;
        }

        public override SecurityTokenProvider CreateSecurityTokenProvider(SecurityTokenRequirement requirement)
        {
            if (requirement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(requirement));
            }

            SecurityTokenProvider result = null;
            if (requirement is RecipientServiceModelSecurityTokenRequirement recipientRequirement)
            {
                result = CreateLocalSecurityTokenProvider(recipientRequirement);
            }
            else if (requirement is InitiatorServiceModelSecurityTokenRequirement)
            {
                result = CreateUncorrelatedDuplexSecurityTokenProvider((InitiatorServiceModelSecurityTokenRequirement)requirement);
            }

            if (result == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SecurityTokenManagerCannotCreateProviderForRequirement, requirement)));
            }
            return result;
        }

        public virtual EndpointIdentity GetIdentityOfSelf(SecurityTokenRequirement tokenRequirement)
        {
            if (tokenRequirement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenRequirement));
            }
            if (tokenRequirement is RecipientServiceModelSecurityTokenRequirement)
            {
                string tokenType = tokenRequirement.TokenType;
                if (tokenType == SecurityTokenTypes.X509Certificate
                    || tokenType == ServiceModelSecurityTokenTypes.AnonymousSslnego
                    || tokenType == ServiceModelSecurityTokenTypes.MutualSslnego)
                {
                    if (ServiceCredentials.ServiceCertificate.Certificate != null)
                    {
                        return EndpointIdentity.CreateX509CertificateIdentity(ServiceCredentials.ServiceCertificate.Certificate);
                    }
                }
                else if (tokenType == SecurityTokenTypes.Kerberos || tokenType == ServiceModelSecurityTokenTypes.Spnego)
                {
                    // TODO: Add WindowsIdentity support here as it looks like it is doable
                    throw new PlatformNotSupportedException("WindowsIdentity");
                    //return SecurityUtils.CreateWindowsIdentity();
                }
                else if (tokenType == ServiceModelSecurityTokenTypes.SecureConversation)
                {
                    throw new PlatformNotSupportedException("SecureConversation");
                    //SecurityBindingElement securityBindingElement = ((RecipientServiceModelSecurityTokenRequirement)tokenRequirement).SecureConversationSecurityBindingElement;
                    //if (securityBindingElement != null)
                    //{
                    //    if (securityBindingElement == null || securityBindingElement is TransportSecurityBindingElement)
                    //    {
                    //        return null;
                    //    }
                    //    SecurityTokenParameters bootstrapProtectionParameters = (securityBindingElement is SymmetricSecurityBindingElement) ? ((SymmetricSecurityBindingElement)securityBindingElement).ProtectionTokenParameters : ((AsymmetricSecurityBindingElement)securityBindingElement).RecipientTokenParameters;
                    //    SecurityTokenRequirement bootstrapRequirement = new RecipientServiceModelSecurityTokenRequirement();
                    //    bootstrapProtectionParameters.InitializeSecurityTokenRequirement(bootstrapRequirement);
                    //    return GetIdentityOfSelf(bootstrapRequirement);
                    //}
                }
            }
            return null;
        }
    }
}
