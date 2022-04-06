// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using Claim = System.Security.Claims.Claim;

namespace CoreWCF.IdentityModel
{
    internal static class X509Util
    {
        /// <summary>
        /// Creates an X509CertificateValidator using the given parameters.
        /// </summary>
        /// <param name="certificateValidationMode">The certificate validation mode to use.</param>
        /// <param name="revocationMode">The revocation mode to use.</param>
        /// <param name="trustedStoreLocation">The store to use.</param>
        /// <returns>The X509CertificateValidator.</returns>
        /// <remarks>Due to a WCF bug, X509CertificateValidatorEx must be used rather than WCF's validators directly</remarks>
        internal static X509CertificateValidator CreateCertificateValidator(
            X509CertificateValidationMode certificateValidationMode,
            X509RevocationMode revocationMode,
            StoreLocation trustedStoreLocation)
        {
            return new X509CertificateValidatorEx(certificateValidationMode, revocationMode, trustedStoreLocation);
        }

        internal static string GetCertificateId(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            string certificateId = certificate.SubjectName.Name;
            if (string.IsNullOrEmpty(certificateId))
            {
                certificateId = certificate.Thumbprint;
            }

            return certificateId;
        }

        internal static string GetCertificateIssuerName(X509Certificate2 certificate, IssuerNameRegistry issuerNameRegistry)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            if (issuerNameRegistry == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(issuerNameRegistry));
            }

            X509Chain chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(certificate);
            X509ChainElementCollection elements = chain.ChainElements;

            string issuer = null;
            if (elements.Count > 1)
            {
                using (X509SecurityToken token = new X509SecurityToken(elements[1].Certificate))
                {
                    issuer = issuerNameRegistry.GetIssuerName(token);
                }
            }
            else
            {
                // This is a self-issued certificate. Use the thumbprint of the current certificate.
                using (X509SecurityToken token = new X509SecurityToken(certificate))
                {
                    issuer = issuerNameRegistry.GetIssuerName(token);
                }
            }

            for (int i = 1; i < elements.Count; ++i)
            {
                // Resets the state of the certificate and frees resources associated with it.
                elements[i].Certificate.Reset();
            }

            return issuer;
        }

        public static IEnumerable<Claim> GetClaimsFromCertificate(X509Certificate2 certificate, string issuer)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            ICollection<Claim> claimsCollection = new Collection<Claim>();

            string thumbprint = Convert.ToBase64String(certificate.GetCertHash());
            claimsCollection.Add(new Claim(ClaimTypes.Thumbprint, thumbprint, ClaimValueTypes.Base64Binary, issuer));

            string value = certificate.SubjectName.Name;
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.X500DistinguishedName, value, ClaimValueTypes.String, issuer));
            }

            value = certificate.GetNameInfo(X509NameType.DnsName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.Dns, value, ClaimValueTypes.String, issuer));
            }

            value = certificate.GetNameInfo(X509NameType.SimpleName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.Name, value, ClaimValueTypes.String, issuer));
            }

            value = certificate.GetNameInfo(X509NameType.EmailName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.Email, value, ClaimValueTypes.String, issuer));
            }

            value = certificate.GetNameInfo(X509NameType.UpnName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.Upn, value, ClaimValueTypes.String, issuer));
            }

            value = certificate.GetNameInfo(X509NameType.UrlName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.Uri, value, ClaimValueTypes.String, issuer));
            }

            RSA rsa;
           // if (LocalAppContextSwitches.DisableCngCertificates)
            {
                rsa = certificate.PublicKey.Key as RSA;
            }
            //else
            //{
            //    rsa = CngLightup.GetRSAPublicKey(certificate);
            //}
            if (rsa != null)
            {
                claimsCollection.Add(new Claim(ClaimTypes.Rsa, rsa.ToXmlString(false), ClaimValueTypes.RsaKeyValue, issuer));
            }

            DSA dsa;
            //if (LocalAppContextSwitches.DisableCngCertificates)
            {
                dsa = certificate.PublicKey.Key as DSA;
            }
            //else
            //{
            //    dsa = CngLightup.GetDSAPublicKey(certificate);
            //}
            if (dsa != null)
            {
                claimsCollection.Add(new Claim(ClaimTypes.Dsa, dsa.ToXmlString(false), ClaimValueTypes.DsaKeyValue, issuer));
            }

            value = certificate.SerialNumber;
            if (!string.IsNullOrEmpty(value))
            {
                claimsCollection.Add(new Claim(ClaimTypes.SerialNumber, value, ClaimValueTypes.String, issuer));
            }

            return claimsCollection;
        }
    }
}
