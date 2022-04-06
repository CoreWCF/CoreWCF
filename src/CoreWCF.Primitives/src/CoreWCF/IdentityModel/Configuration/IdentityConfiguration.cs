// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Configuration
{
    /// <summary>
    /// Defines the collection of configurable properties controlling the behavior of the Windows Identity Foundation.
    /// </summary>
    public class IdentityConfiguration
    {
        public const string DefaultServiceName = ConfigurationStrings.DefaultServiceName;
        public static readonly TimeSpan DefaultMaxClockSkew = new TimeSpan(0, 5, 0);
        internal const string DefaultMaxClockSkewString = "00:05:00";
        public static readonly X509CertificateValidationMode DefaultCertificateValidationMode = X509CertificateValidationMode.PeerOrChainTrust;
        public static readonly Type DefaultIssuerNameRegistryType = typeof(ConfigurationBasedIssuerNameRegistry);
        public static readonly X509RevocationMode DefaultRevocationMode = X509RevocationMode.Online;
        public static readonly StoreLocation DefaultTrustedStoreLocation = StoreLocation.LocalMachine;
        private TimeSpan _serviceMaxClockSkew = DefaultMaxClockSkew;
        private SecurityTokenHandlerConfiguration _serviceHandlerConfiguration;

        public IdentityConfiguration(IEnumerable<SecurityTokenHandler> securityTokenHandlers)
        {
            LoadHandlersNoConfig(securityTokenHandlers);
        }


        /// <summary>
        /// Gets or sets the AudienceRestriction.
        /// </summary>
        public AudienceRestriction AudienceRestriction
        {
            get { return _serviceHandlerConfiguration.AudienceRestriction; }
            set { _serviceHandlerConfiguration.AudienceRestriction = value; }
        }

        /// <summary>
        /// Gets the Caches configured.
        /// </summary>
        public IdentityModelCaches Caches
        {
            get { return _serviceHandlerConfiguration.Caches; }
            set { _serviceHandlerConfiguration.Caches = value; }
        }

        /// <summary>
        /// Gets or sets the certificate validation mode used by handlers to validate issuer certificates
        /// </summary>
        public X509CertificateValidationMode CertificateValidationMode
        {
            get { return _serviceHandlerConfiguration.CertificateValidationMode; }
            set { _serviceHandlerConfiguration.CertificateValidationMode = value; }
        }

        /// <summary>
        /// Gets or sets the certificate validator used by handlers to validate issuer certificates
        /// </summary>
        public X509CertificateValidator CertificateValidator
        {
            get { return _serviceHandlerConfiguration.CertificateValidator; }
            set { _serviceHandlerConfiguration.CertificateValidator = value; }
        }

        /// <summary>
        /// Gets or Sets detection of replaying of tokens by handlers in the default handler configuration.
        /// </summary>
        public bool DetectReplayedTokens
        {
            get { return _serviceHandlerConfiguration.DetectReplayedTokens; }
            set { _serviceHandlerConfiguration.DetectReplayedTokens = value; }
        }

        /// <summary>
        /// Determines if <see cref="IdentityConfiguration.Initialize"/> has been called.
        /// </summary>
        public virtual bool IsInitialized { get; set; }

        /// <summary>
        /// Updates properties in the <see cref="SecurityTokenHandlerConfiguration"/> objects for the 
        /// <see cref="SecurityTokenHandlerCollection"/> objects contained in 
        /// <see cref="IdentityConfiguration.SecurityTokenHandlerCollectionManager"/> to be consistent with the property
        /// values on this <see cref="IdentityConfiguration"/> instance.
        /// </summary>
        /// <remarks>
        /// This method should be invoked prior to using these token handlers
        /// for token processing.
        /// </remarks>
        /// <exception cref="InvalidOperationException">If this method is invoked more than once.</exception>
        public virtual void Initialize()
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException(SR.Format(SR.ID7009));
            }

            SecurityTokenHandlerCollection defaultCollection = SecurityTokenHandlers;

            if (!object.ReferenceEquals(_serviceHandlerConfiguration, defaultCollection.Configuration))
            {
                //
                // If someone has created their own new STHConfig and set it as default, leave that config alone.
                //
               // TraceUtility.TraceString(TraceEventType.Information, SR.Format(SR.ID4283));
                IsInitialized = true;
                return;
            }

            // Update the ServiceTokenResolver of the default TokenHandlerCollection's configuration, if serviceCertificate is set.
            if (ServiceCertificate != null)
            {
                SecurityTokenResolver serviceCertificateResolver = SecurityTokenResolver.CreateDefaultSecurityTokenResolver(new ReadOnlyCollection<SecurityToken>(
                                                      new SecurityToken[] { new X509SecurityToken(ServiceCertificate) }), false);

                SecurityTokenResolver tokenResolver = SecurityTokenHandlers.Configuration.ServiceTokenResolver;

                if ((tokenResolver != null) && (tokenResolver != EmptySecurityTokenResolver.Instance))
                {
                    SecurityTokenHandlers.Configuration.ServiceTokenResolver = new AggregateTokenResolver(new SecurityTokenResolver[] { serviceCertificateResolver, tokenResolver });
                }
                else
                {
                    SecurityTokenHandlers.Configuration.ServiceTokenResolver = serviceCertificateResolver;
                }
            }

            SecurityTokenResolver configuredIssuerTokenResolver = IssuerTokenResolver;

            if (IssuerTokenResolver == SecurityTokenHandlerConfiguration.DefaultIssuerTokenResolver)
            {
                //
                // Add the known certificates from WCF's ServiceCredentials in front of 
                // the default issuer token resolver.
                //
                if (KnownIssuerCertificates != null)
                {
                    int count = KnownIssuerCertificates.Count;
                    if (count > 0)
                    {
                        SecurityToken[] tokens = new SecurityToken[count];
                        for (int i = 0; i < count; i++)
                        {
                            tokens[i] = new X509SecurityToken(KnownIssuerCertificates[i]);
                        }

                        SecurityTokenResolver knownCertificateTokenResolver = SecurityTokenResolver.CreateDefaultSecurityTokenResolver(new ReadOnlyCollection<SecurityToken>(tokens), false);
                        
                        IssuerTokenResolver = new AggregateTokenResolver(new SecurityTokenResolver[] { knownCertificateTokenResolver, configuredIssuerTokenResolver });                       
                    }
                }
            }
            
            if (CertificateValidationMode != X509CertificateValidationMode.Custom)
            {
                defaultCollection.Configuration.CertificateValidator = X509Util.CreateCertificateValidator(defaultCollection.Configuration.CertificateValidationMode,
                                                                                                            defaultCollection.Configuration.RevocationMode,
                                                                                                            defaultCollection.Configuration.TrustedStoreLocation);
            }
            else if (object.ReferenceEquals(defaultCollection.Configuration.CertificateValidator, SecurityTokenHandlerConfiguration.DefaultCertificateValidator))
            {
                //
                // If the mode is custom but the validator or still default, something has gone wrong.
                //
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID4280)));
            }

            IsInitialized = true;
        }

        /// <summary>
        /// TODO : Discuss better way to separate the configurations from Primitives
        /// </summary>
        private void LoadHandlersNoConfig(IEnumerable<SecurityTokenHandler> serviceTokenHandlers)
        {
            SecurityTokenHandlerCollectionManager manager = SecurityTokenHandlerCollectionManager.CreateEmptySecurityTokenHandlerCollectionManager();
            _serviceHandlerConfiguration = new SecurityTokenHandlerConfiguration
            {
                MaxClockSkew = _serviceMaxClockSkew
            };

            if (!manager.ContainsKey(SecurityTokenHandlerCollectionManager.Usage.Default))
            {
                manager[SecurityTokenHandlerCollectionManager.Usage.Default] = SecurityTokenHandlerCollection.CreateDefaultSecurityTokenHandlerCollection(_serviceHandlerConfiguration, serviceTokenHandlers);
            }
            SecurityTokenHandlerCollectionManager = manager;
        }

        /// <summary>
        /// Gets or sets the maximum allowable time difference between the 
        /// system clocks of the two parties that are communicating.
        /// </summary>
        public TimeSpan MaxClockSkew
        {
            get { return _serviceHandlerConfiguration.MaxClockSkew; }
            set { _serviceHandlerConfiguration.MaxClockSkew = value; }
        }

        /// <summary>
        /// Gets or sets the service name of this configuration.
        /// </summary>
        public string Name { get; } = DefaultServiceName;

        /// <summary>
        /// Gets or sets the IssuerNameRegistry used to resolve issuer names.
        /// </summary>
        public IssuerNameRegistry IssuerNameRegistry
        {
            get
            {
                return _serviceHandlerConfiguration.IssuerNameRegistry;
            }
            set
            {
                _serviceHandlerConfiguration.IssuerNameRegistry = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// The service certificate to initialize the ServiceTokenResolver and the SessionSecurityTokenHandler.
        /// </summary>
        public X509Certificate2 ServiceCertificate { get; set; }

        internal List<X509Certificate2> KnownIssuerCertificates { get; set; }


        /// <summary>
        /// Gets or Sets the Issuer token resolver.
        /// </summary>
        public SecurityTokenResolver IssuerTokenResolver
        {
            get
            {
                return _serviceHandlerConfiguration.IssuerTokenResolver;
            }
            set
            {
                _serviceHandlerConfiguration.IssuerTokenResolver = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the revocation mode used by handlers to validate issuer certificates
        /// </summary>
        public X509RevocationMode RevocationMode
        {
            get { return _serviceHandlerConfiguration.RevocationMode; }
            set { _serviceHandlerConfiguration.RevocationMode = value; }
        }

        /// <summary>
        /// Gets or Sets the Service token resolver.
        /// </summary>
        public SecurityTokenResolver ServiceTokenResolver
        {
            get
            {
                return _serviceHandlerConfiguration.ServiceTokenResolver;
            }
            set
            {
                _serviceHandlerConfiguration.ServiceTokenResolver = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets if BootstrapContext is saved in the ClaimsIdentity and Sessions after token validation.
        /// </summary>
        public bool SaveBootstrapContext
        {
            get { return _serviceHandlerConfiguration.SaveBootstrapContext; }
            set { _serviceHandlerConfiguration.SaveBootstrapContext = value; }
        }

        /// <summary>
        /// The <see cref="SecurityTokenHandlerCollectionManager" /> containing the set of <see cref="SecurityTokenHandler" />
        /// objects used for serializing and validating tokens found in WS-Trust messages.
        /// </summary>
        public SecurityTokenHandlerCollectionManager SecurityTokenHandlerCollectionManager { get; private set; }

        /// <summary>
        /// The <see cref="SecurityTokenHandlerCollection" /> collection of <see cref="SecurityTokenHandler" />
        /// objects used for serializing and validating tokens found in WS-Trust messages.
        /// If user wants to register their own token handler, they
        /// can simply add their own handler to this collection.
        /// </summary>
        public SecurityTokenHandlerCollection SecurityTokenHandlers
        {
            get
            {
                return SecurityTokenHandlerCollectionManager[SecurityTokenHandlerCollectionManager.Usage.Default];
            }
        }

        /// <summary>
        /// Gets or Sets the expiration period for items placed in the TokenReplayCache.
        /// </summary>
        public TimeSpan TokenReplayCacheExpirationPeriod
        {
            get { return _serviceHandlerConfiguration.TokenReplayCacheExpirationPeriod; }
            set { _serviceHandlerConfiguration.TokenReplayCacheExpirationPeriod = value; }
        }

        /// <summary>
        /// Gets or sets the trusted store location used by handlers to validate issuer certificates
        /// </summary>
        public StoreLocation TrustedStoreLocation
        {
            get { return _serviceHandlerConfiguration.TrustedStoreLocation; }
            set { _serviceHandlerConfiguration.TrustedStoreLocation = value; }
        }
    }
}
