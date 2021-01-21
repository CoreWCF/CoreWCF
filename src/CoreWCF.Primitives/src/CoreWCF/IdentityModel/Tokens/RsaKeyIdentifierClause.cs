// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    public class RsaKeyIdentifierClause : SecurityKeyIdentifierClause
    {
        private static readonly string clauseType = XmlSignatureStrings.Namespace + XmlSignatureStrings.RsaKeyValue;
        private readonly RSA rsa;
        private readonly RSAParameters rsaParameters;
        private RsaSecurityKey rsaSecurityKey;

        public RsaKeyIdentifierClause(RSA rsa)
            : base(clauseType)
        {
            if (rsa == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rsa));
            }

            this.rsa = rsa;
            rsaParameters = rsa.ExportParameters(false);
        }

        public override bool CanCreateKey
        {
            get { return true; }
        }

        public RSA Rsa
        {
            get { return rsa; }
        }

        public override SecurityKey CreateKey()
        {
            if (rsaSecurityKey == null)
            {
                rsaSecurityKey = new RsaSecurityKey(rsa);
            }
            return rsaSecurityKey;
        }

        public byte[] GetExponent()
        {
            return SecurityUtils.CloneBuffer(rsaParameters.Exponent);
        }

        public byte[] GetModulus()
        {
            return SecurityUtils.CloneBuffer(rsaParameters.Modulus);
        }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            RsaKeyIdentifierClause that = keyIdentifierClause as RsaKeyIdentifierClause;
            return ReferenceEquals(this, that) || (that != null && that.Matches(rsa));
        }

        public bool Matches(RSA rsa)
        {
            if (rsa == null)
            {
                return false;
            }

            RSAParameters rsaParameters = rsa.ExportParameters(false);
            return SecurityUtils.MatchesBuffer(this.rsaParameters.Modulus, rsaParameters.Modulus) &&
                SecurityUtils.MatchesBuffer(this.rsaParameters.Exponent, rsaParameters.Exponent);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "RsaKeyIdentifierClause(Modulus = {0}, Exponent = {1})",
                Convert.ToBase64String(rsaParameters.Modulus),
                Convert.ToBase64String(rsaParameters.Exponent));
        }

        public void WriteExponentAsBase64(XmlWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }
            writer.WriteBase64(rsaParameters.Exponent, 0, rsaParameters.Exponent.Length);
        }

        public void WriteModulusAsBase64(XmlWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }
            writer.WriteBase64(rsaParameters.Modulus, 0, rsaParameters.Modulus.Length);
        }
    }
}
