// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System;

namespace CoreWCF.IdentityModel.Tokens
{
    public class ConfigurationBasedIssuerNameRegistry : IssuerNameRegistry
    {
        private readonly Dictionary<string, string> _configuredTrustedIssuers = new Dictionary<string, string>(new ThumbprintKeyComparer());

        /// <summary>
        /// Creates an instance of <see cref="ConfigurationBasedIssuerNameRegistry"/>
        /// </summary>
        public ConfigurationBasedIssuerNameRegistry()
        {
        }

        /// <summary>
        /// Returns the issuer name of the given X509SecurityToken mapping the Certificate Thumbprint to 
        /// a name in the configured map.
        /// </summary>
        /// <param name="securityToken">SecurityToken for which the issuer name is requested.</param>
        /// <returns>Issuer name if the token was registered, null otherwise.</returns>
        /// <exception cref="ArgumentNullException">The input parameter 'securityToken' is null.</exception>
        public override string GetIssuerName(SecurityToken securityToken)
        {
            if (securityToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityToken));
            }

            if (securityToken is X509SecurityToken x509SecurityToken)
            {
                string thumbprint = x509SecurityToken.Certificate.Thumbprint;
                if (_configuredTrustedIssuers.ContainsKey(thumbprint))
                {
                    string issuerName = _configuredTrustedIssuers[thumbprint];
                    issuerName = string.IsNullOrEmpty(issuerName) ? x509SecurityToken.Certificate.Subject : issuerName;

                    //if (TD.GetIssuerNameSuccessIsEnabled())
                    //{
                    //    TD.GetIssuerNameSuccess(EventTraceActivity.GetFromThreadOrCreate(), issuerName, securityToken.Id);
                    //}

                    return issuerName;
                }
            }

            //if (TD.GetIssuerNameFailureIsEnabled())
            //{
            //    TD.GetIssuerNameFailure(EventTraceActivity.GetFromThreadOrCreate(), securityToken.Id);
            //}

            return null;
        }

        /// <summary>
        /// Gets the Dictionary of Configured Trusted Issuers. The key
        /// to the dictionary is the ASN.1 encoded form of the Thumbprint 
        /// of the trusted issuer's certificate and the value is the issuer name. 
        /// </summary>
        public IDictionary<string, string> ConfiguredTrustedIssuers
        {
            get { return _configuredTrustedIssuers; }
        }

        /// <summary>
        /// Adds a trusted issuer to the collection.
        /// </summary>
        /// <param name="certificateThumbprint">ASN.1 encoded form of the trusted issuer's certificate Thumbprint.</param>
        /// <param name="name">Name of the trusted issuer.</param>
        /// <exception cref="ArgumentException">The argument 'certificateThumbprint' or 'name' is either null or Empty.</exception>
        /// <exception cref="InvalidOperationException">The issuer specified by 'certificateThumbprint' argument has already been configured.</exception>
        public void AddTrustedIssuer(string certificateThumbprint, string name)
        {
            if (string.IsNullOrEmpty(certificateThumbprint))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificateThumbprint));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (_configuredTrustedIssuers.ContainsKey(certificateThumbprint))
            {
                throw new InvalidOperationException(SR.Format(SR.ID4265, certificateThumbprint));
            }

            certificateThumbprint = certificateThumbprint.Replace(" ", "");

            _configuredTrustedIssuers.Add(certificateThumbprint, name);
        }

        private class ThumbprintKeyComparer : IEqualityComparer<string>
        {
            #region IEqualityComparer<string> Members

            public bool Equals(string x, string y)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(x, y);
            }

            public int GetHashCode(string obj)
            {
                return obj.ToUpper(CultureInfo.InvariantCulture).GetHashCode();
            }

            #endregion
        }
    }
}
