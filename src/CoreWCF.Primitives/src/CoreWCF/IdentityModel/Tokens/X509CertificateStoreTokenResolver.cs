// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Token Resolver that can resolve X509SecurityTokens against a given X.509 Certificate Store.
    /// </summary>
    public class X509CertificateStoreTokenResolver : SecurityTokenResolver
    {
        /// <summary>
        /// Initializes an instance of <see cref="X509CertificateStoreTokenResolver"/>
        /// </summary>
        public X509CertificateStoreTokenResolver()
            : this(System.Security.Cryptography.X509Certificates.StoreName.My, StoreLocation.LocalMachine)
        {
        }

        /// <summary>
        /// Initializes an instance of <see cref="X509CertificateStoreTokenResolver"/>
        /// </summary>
        /// <param name="storeName">StoreName of the X.509 Certificate Store.</param>
        /// <param name="storeLocation">StoreLocation of the X.509 Certificate store.</param>
        public X509CertificateStoreTokenResolver(StoreName storeName, StoreLocation storeLocation)
            : this(Enum.GetName(typeof(System.Security.Cryptography.X509Certificates.StoreName), storeName), storeLocation)
        {
        }

        /// <summary>
        /// Initializes an instance of <see cref="X509CertificateStoreTokenResolver"/>
        /// </summary>
        /// <param name="storeName">StoreName of the X.509 Certificate Store.</param>
        /// <param name="storeLocation">StoreLocation of the X.509 Certificate store.</param>
        public X509CertificateStoreTokenResolver(string storeName, StoreLocation storeLocation)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(storeName));
            }

            StoreName = storeName;
            StoreLocation = storeLocation;
        }

        /// <summary>
        /// Gets the StoreName used by this TokenResolver.
        /// </summary>
        public string StoreName { get; }

        /// <summary>
        /// Gets the StoreLocation used by this TokenResolver.
        /// </summary>
        public StoreLocation StoreLocation { get; }

        /// <summary>
        /// Resolves the given SecurityKeyIdentifierClause to a SecurityKey.
        /// </summary>
        /// <param name="keyIdentifierClause">SecurityKeyIdentifierClause to resolve</param>
        /// <param name="key">The resolved SecurityKey.</param>
        /// <returns>True if successfully resolved.</returns>
        /// <exception cref="ArgumentNullException">The input argument 'keyIdentifierClause' is null.</exception>
        protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }

            key = null;
            if (keyIdentifierClause is EncryptedKeyIdentifierClause encryptedKeyIdentifierClause)
            {
                SecurityKeyIdentifier keyIdentifier = encryptedKeyIdentifierClause.EncryptingKeyIdentifier;
                if (keyIdentifier != null && keyIdentifier.Count > 0)
                {
                    for (int i = 0; i < keyIdentifier.Count; i++)
                    {
                        if (TryResolveSecurityKey(keyIdentifier[i], out SecurityKey unwrappingSecurityKey))
                        {
                            byte[] wrappedKey = encryptedKeyIdentifierClause.GetEncryptedKey();
                            string wrappingAlgorithm = encryptedKeyIdentifierClause.EncryptionMethod;
                            byte[] unwrappedKey = unwrappingSecurityKey.DecryptKey(wrappingAlgorithm, wrappedKey);
                            key = new InMemorySymmetricSecurityKey(unwrappedKey, false);
                            return true;
                        }
                    }
                }
            }
            else
            {
                SecurityToken token = null;
                if (TryResolveToken(keyIdentifierClause, out token))
                {
                    if (token.SecurityKeys.Count > 0)
                    {
                        key = token.SecurityKeys[0];
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the given SecurityKeyIdentifier to a SecurityToken.
        /// </summary>
        /// <param name="keyIdentifier">SecurityKeyIdentifier to resolve.</param>
        /// <param name="token">The resolved SecurityToken.</param>
        /// <returns>True if successfully resolved.</returns>
        /// <exception cref="ArgumentNullException">The input argument 'keyIdentifier' is null.</exception>
        protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
        {
            if (keyIdentifier == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));
            }

            token = null;
            foreach (SecurityKeyIdentifierClause clause in keyIdentifier)
            {
                if (TryResolveToken(clause, out token))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the given SecurityKeyIdentifierClause to a SecurityToken.
        /// </summary>
        /// <param name="keyIdentifierClause">SecurityKeyIdentifierClause to resolve.</param>
        /// <param name="token">The resolved SecurityToken.</param>
        /// <returns>True if successfully resolved.</returns>
        /// <exception cref="ArgumentNullException">The input argument 'keyIdentifierClause' is null.</exception>
        protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }

            token = null;
            X509Store store = null;
            X509Certificate2Collection certs = null;
            try
            {
                store = new X509Store(StoreName, StoreLocation);
                store.Open(OpenFlags.ReadOnly);
                certs = store.Certificates;
                foreach (X509Certificate2 cert in certs)
                {
                    if (keyIdentifierClause is X509ThumbprintKeyIdentifierClause thumbprintKeyIdentifierClause && thumbprintKeyIdentifierClause.Matches(cert))
                    {
                        token = new X509SecurityToken(cert);
                        return true;
                    }

                    if (keyIdentifierClause is X509IssuerSerialKeyIdentifierClause issuerSerialKeyIdentifierClause && issuerSerialKeyIdentifierClause.Matches(cert))
                    {
                        token = new X509SecurityToken(cert);
                        return true;
                    }

                    if (keyIdentifierClause is X509SubjectKeyIdentifierClause subjectKeyIdentifierClause && subjectKeyIdentifierClause.Matches(cert))
                    {
                        token = new X509SecurityToken(cert);
                        return true;
                    }

                    if (keyIdentifierClause is X509RawDataKeyIdentifierClause rawDataKeyIdentifierClause && rawDataKeyIdentifierClause.Matches(cert))
                    {
                        token = new X509SecurityToken(cert);
                        return true;
                    }
                }
            }
            finally
            {
                if (certs != null)
                {
                    for (int i = 0; i < certs.Count; i++)
                    {
                        certs[i].Reset();
                    }
                }

                if (store != null)
                {
                    store.Close();
                }
            }

            return false;
        }
    }
}
