// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Resolves issuer tokens received from service partners.
    /// </summary>
    public class IssuerTokenResolver : SecurityTokenResolver
    {
        /// <summary>
        /// Default store for resolving X509 certificates.
        /// </summary>
        public static readonly StoreName DefaultStoreName = StoreName.TrustedPeople;
        /// <summary>
        /// Default store location for resolving X509 certificates.
        /// </summary>
        public static readonly StoreLocation DefaultStoreLocation = StoreLocation.LocalMachine;
        internal static IssuerTokenResolver s_defaultInstance = new IssuerTokenResolver();

        /// <summary>
        /// Creates an instance of IssuerTokenResolver.
        /// </summary>
        public IssuerTokenResolver()
            : this( new X509CertificateStoreTokenResolver( DefaultStoreName, DefaultStoreLocation ) )
        {
        }

        /// <summary>
        /// Creates an instance of IssuerTokenResolver using a given <see cref="SecurityTokenResolver"/>.
        /// </summary>
        /// <param name="wrappedTokenResolver">The <see cref="SecurityTokenResolver"/> to use.</param>
        public IssuerTokenResolver( SecurityTokenResolver wrappedTokenResolver )
        {
            WrappedTokenResolver = wrappedTokenResolver ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull( nameof(wrappedTokenResolver) );
        }

        /// <summary>
        /// Gets the <see cref="SecurityTokenResolver"/> wrapped by this class.
        /// </summary>
        public SecurityTokenResolver WrappedTokenResolver { get; } = null;

        /// <summary>
        /// Inherited from <see cref="SecurityTokenResolver"/>.
        /// </summary>
        protected override bool TryResolveSecurityKeyCore( SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key )
        {
            if ( keyIdentifierClause == null )
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull( nameof(keyIdentifierClause) );
            }

            if (keyIdentifierClause is X509RawDataKeyIdentifierClause rawDataClause)
            {
                key = rawDataClause.CreateKey();
                return true;
            }

            if (keyIdentifierClause is RsaKeyIdentifierClause rsaClause)
            {
                key = rsaClause.CreateKey();
                return true;
            }

            if ( WrappedTokenResolver.TryResolveSecurityKey( keyIdentifierClause, out key ) )
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Inherited from <see cref="SecurityTokenResolver"/>.
        /// </summary>
        protected override bool TryResolveTokenCore( SecurityKeyIdentifier keyIdentifier, out SecurityToken token )
        {
            if ( keyIdentifier == null )
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));
            }

            token = null;
            foreach ( SecurityKeyIdentifierClause clause in keyIdentifier )
            {
                if ( TryResolveTokenCore( clause, out token ) )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Inherited from <see cref="SecurityTokenResolver"/>.
        /// </summary>
        protected override bool TryResolveTokenCore( SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token )
        {
            if ( keyIdentifierClause == null )
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull( nameof(keyIdentifierClause) );
            }


            //
            // Try raw X509
            //
            if (keyIdentifierClause is X509RawDataKeyIdentifierClause rawDataClause)
            {
                token = new X509SecurityToken(new X509Certificate2(rawDataClause.GetX509RawData()));
                return true;
            }

            //
            // Try RSA
            //
            if (keyIdentifierClause is RsaKeyIdentifierClause rsaClause)
            {
                token = new RsaSecurityToken(rsaClause.Rsa);
                return true;
            }

            if ( WrappedTokenResolver.TryResolveToken( keyIdentifierClause, out token ) )
            {
                return true;
            }
            
            return false;
        }
    }
}
