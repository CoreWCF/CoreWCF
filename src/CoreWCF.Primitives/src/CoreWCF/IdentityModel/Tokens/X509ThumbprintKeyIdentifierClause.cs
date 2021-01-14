﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    public class X509ThumbprintKeyIdentifierClause : BinaryKeyIdentifierClause
    {
        public X509ThumbprintKeyIdentifierClause(X509Certificate2 certificate)
            : this(GetHash(certificate), false)
        {
        }

        public X509ThumbprintKeyIdentifierClause(byte[] thumbprint)
            : this(thumbprint, true)
        {
        }

        internal X509ThumbprintKeyIdentifierClause(byte[] thumbprint, bool cloneBuffer)
            : base(null, thumbprint, cloneBuffer)
        {
        }

        static byte[] GetHash(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            //TODO  switch to stronger hash algorithm once we're no longer on netstandard2.0
            return certificate.GetCertHash();
        }

        public byte[] GetX509Thumbprint()
        {
            return GetBuffer();
        }

        public bool Matches(X509Certificate2 certificate)
        {
            if (certificate == null)
                return false;

            return Matches(GetHash(certificate));
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "X509ThumbprintKeyIdentifierClause(Hash = 0x{0})", ToHexString());
        }
    }
}
