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
        private static readonly string s_clauseType = XmlSignatureStrings.Namespace + XmlSignatureStrings.RsaKeyValue;
        private readonly RSAParameters _rsaParameters;
        private RsaSecurityKey _rsaSecurityKey;

        public RsaKeyIdentifierClause(RSA rsa)
            : base(s_clauseType)
        {
            if (rsa == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rsa));
            }

            Rsa = rsa;
            _rsaParameters = rsa.ExportParameters(false);
        }

        public override bool CanCreateKey
        {
            get { return true; }
        }

        public RSA Rsa { get; }

        public override SecurityKey CreateKey()
        {
            if (_rsaSecurityKey == null)
            {
                _rsaSecurityKey = new RsaSecurityKey(Rsa);
            }
            return _rsaSecurityKey;
        }

        public byte[] GetExponent()
        {
            return SecurityUtils.CloneBuffer(_rsaParameters.Exponent);
        }

        public byte[] GetModulus()
        {
            return SecurityUtils.CloneBuffer(_rsaParameters.Modulus);
        }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            RsaKeyIdentifierClause that = keyIdentifierClause as RsaKeyIdentifierClause;
            return ReferenceEquals(this, that) || (that != null && that.Matches(Rsa));
        }

        public bool Matches(RSA rsa)
        {
            if (rsa == null)
            {
                return false;
            }

            RSAParameters rsaParameters = rsa.ExportParameters(false);
            return SecurityUtils.MatchesBuffer(_rsaParameters.Modulus, rsaParameters.Modulus) &&
                SecurityUtils.MatchesBuffer(_rsaParameters.Exponent, rsaParameters.Exponent);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "RsaKeyIdentifierClause(Modulus = {0}, Exponent = {1})",
                Convert.ToBase64String(_rsaParameters.Modulus),
                Convert.ToBase64String(_rsaParameters.Exponent));
        }

        public void WriteExponentAsBase64(XmlWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }
            writer.WriteBase64(_rsaParameters.Exponent, 0, _rsaParameters.Exponent.Length);
        }

        public void WriteModulusAsBase64(XmlWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }
            writer.WriteBase64(_rsaParameters.Modulus, 0, _rsaParameters.Modulus.Length);
        }
    }
}
