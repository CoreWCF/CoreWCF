// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Configuration;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Configuration common to all SecurityTokenHandlers.
    /// </summary>
    public class SecurityTokenHandlerConfiguration
    {
        /// <summary>
        /// Gets a value indicating whether or not to detect replay tokens by default.
        /// </summary>
        public static readonly bool DefaultDetectReplayedTokens; // false

        /// <summary>
        /// Gets the default issuer name registry.
        /// </summary>
        public static readonly IssuerNameRegistry DefaultIssuerNameRegistry = new ConfigurationBasedIssuerNameRegistry();

        /// <summary>
        /// Gets the default issuer token resolver.
        /// </summary>
        public static readonly SecurityTokenResolver DefaultIssuerTokenResolver = CoreWCF.IdentityModel.Tokens.IssuerTokenResolver.s_defaultInstance;

        /// <summary>
        /// Gets the default maximum clock skew.
        /// </summary>
        public static readonly TimeSpan DefaultMaxClockSkew = new TimeSpan(0, 5, 0); // 5 minutes

        /// <summary>
        /// Gets a value indicating whether or not to save bootstrap tokens by default.
        /// </summary>
        public static readonly bool DefaultSaveBootstrapContext; // false;

        /// <summary>
        /// Gets the default token replay cache expiration period.
        /// </summary>
        public static readonly TimeSpan DefaultTokenReplayCacheExpirationPeriod = TimeSpan.MaxValue;

        // The below 3 defaults were moved from  IdentityConfiguration class as we can not have service configuration in IdentityModel.

        /// <summary>
        /// Gets the default X.509 certificate validation mode.
        /// </summary>
        public static readonly X509CertificateValidationMode DefaultCertificateValidationMode = IdentityConfiguration.DefaultCertificateValidationMode;

        /// <summary>
        /// Gets the default X.509 certificate revocation validation mode.
        /// </summary>
        public static readonly X509RevocationMode DefaultRevocationMode = IdentityConfiguration.DefaultRevocationMode;

        /// <summary>
        /// Gets the default X.509 certificate trusted store location.
        /// </summary>
        public static readonly StoreLocation DefaultTrustedStoreLocation = IdentityConfiguration.DefaultTrustedStoreLocation;
        X509CertificateValidationMode certificateValidationMode = DefaultCertificateValidationMode;

        /// <summary>
        /// Gets the default X.509 certificate validator instance.
        /// </summary>
        public static readonly X509CertificateValidator DefaultCertificateValidator = X509Util.CreateCertificateValidator(DefaultCertificateValidationMode, DefaultRevocationMode, DefaultTrustedStoreLocation);
        private AudienceRestriction _audienceRestriction = new AudienceRestriction();
        private X509CertificateValidator _certificateValidator = DefaultCertificateValidator;
        private IssuerNameRegistry _issuerNameRegistry = DefaultIssuerNameRegistry;
        private SecurityTokenResolver _issuerTokenResolver = DefaultIssuerTokenResolver;
        private TimeSpan _maxClockSkew = DefaultMaxClockSkew;
        private SecurityTokenResolver _serviceTokenResolver = EmptySecurityTokenResolver.Instance;
        private TimeSpan _tokenReplayCacheExpirationPeriod = DefaultTokenReplayCacheExpirationPeriod;
        private IdentityModelCaches _caches = new IdentityModelCaches();
                
        /// <summary>
        /// Creates an instance of <see cref="SecurityTokenHandlerConfiguration"/>
        /// </summary>
        public SecurityTokenHandlerConfiguration()
        {
        }

        /// <summary>
        /// Gets or sets the AudienceRestriction.
        /// </summary>
        public AudienceRestriction AudienceRestriction
        {
            get
            {
                return _audienceRestriction;
            }

            set
            {
                _audienceRestriction = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the certificate validator used by handlers to validate issuer certificates
        /// </summary>
        public X509CertificateValidator CertificateValidator
        {
            get
            {
                return _certificateValidator;
            }

            set
            {
                _certificateValidator = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public X509RevocationMode RevocationMode { get; set; } = DefaultRevocationMode;

        /// <summary>
        /// Gets or sets the trusted store location used by handlers to validate issuer certificates
        /// </summary>
        public StoreLocation TrustedStoreLocation { get; set; } = DefaultTrustedStoreLocation;

        /// <summary>
        /// Gets or sets the certificate validation mode used by handlers to validate issuer certificates
        /// </summary>
        public X509CertificateValidationMode CertificateValidationMode
        {
            get { return certificateValidationMode; }
            set { certificateValidationMode = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to detect replaying of tokens by handlers in this configuration.
        /// </summary>
        public bool DetectReplayedTokens { get; set; } = DefaultDetectReplayedTokens;

        /// <summary>
        /// Gets or sets the IssuerNameRegistry.
        /// </summary>
        public IssuerNameRegistry IssuerNameRegistry
        {
            get 
            {
                return _issuerNameRegistry; 
            }

            set
            {
                _issuerNameRegistry = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the IssuerTokenResolver.
        /// </summary>
        public SecurityTokenResolver IssuerTokenResolver
        {
            get 
            {
                return _issuerTokenResolver; 
            }

            set
            {
                _issuerTokenResolver = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the maximum clock skew for handlers using this config.
        /// </summary>
        public TimeSpan MaxClockSkew
        {
            get 
            {
                return _maxClockSkew; 
            }

            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.ID2070));
                }

                _maxClockSkew = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether BootstrapContext is saved in the ClaimsIdentity and Sessions after token validation.
        /// </summary>
        public bool SaveBootstrapContext { get; set; } = DefaultSaveBootstrapContext;

        /// <summary>
        /// Gets or sets the TokenResolver that resolves Service tokens.
        /// </summary>
        public SecurityTokenResolver ServiceTokenResolver
        {
            get 
            {
                return _serviceTokenResolver; 
            }

            set
            {
                _serviceTokenResolver = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the Caches that are used.
        /// </summary>
        public IdentityModelCaches Caches
        {
            get 
            {
                return _caches; 
            }

            set
            {
                _caches = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the expiration period for items placed in the TokenReplayCache.
        /// </summary>
        public TimeSpan TokenReplayCacheExpirationPeriod
        {
            get 
            {
                return _tokenReplayCacheExpirationPeriod; 
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(value), SR.Format(SR.ID0016));
                }

                _tokenReplayCacheExpirationPeriod = value;
            }
        }
    }
}
