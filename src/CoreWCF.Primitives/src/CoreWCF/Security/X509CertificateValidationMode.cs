// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Security
{
    /// <summary>
    /// An enumeration that lists the ways of validating a certificate.
    /// </summary>
    public enum X509CertificateValidationMode
    {
        /// <summary>
        /// No validation of the certificate is performed. 
        /// </summary>
        None,

        /// <summary>
        /// The certificate is valid if it is in the trusted people store.
        /// </summary>
        PeerTrust,

        /// <summary>
        /// The certificate is valid if the chain builds to a certification authority in the trusted root store.
        /// </summary>
        ChainTrust,

        /// <summary>
        /// The certificate is valid if it is in the trusted people store, or if the chain builds to a certification authority in the trusted root store.
        /// </summary>
        PeerOrChainTrust,

        /// <summary>
        /// The user must plug in a custom <c>X509CertificateValidator</c> to validate the certificate.
        /// </summary>
        Custom
    }

    internal static class X509CertificateValidationModeHelper
    {
        public static bool IsDefined(X509CertificateValidationMode validationMode)
        {
            return validationMode == X509CertificateValidationMode.None
                || validationMode == X509CertificateValidationMode.PeerTrust
                || validationMode == X509CertificateValidationMode.ChainTrust
                || validationMode == X509CertificateValidationMode.PeerOrChainTrust
                || validationMode == X509CertificateValidationMode.Custom;
        }

        internal static void Validate(X509CertificateValidationMode value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(X509CertificateValidationMode)));
            }
        }
    }
}
