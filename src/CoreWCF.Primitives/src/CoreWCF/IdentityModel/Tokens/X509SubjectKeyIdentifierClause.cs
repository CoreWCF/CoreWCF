// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    public class X509SubjectKeyIdentifierClause : BinaryKeyIdentifierClause
    {
        private const string SubjectKeyIdentifierOid = "2.5.29.14";
        private const int SkiDataOffset = 2;

        public X509SubjectKeyIdentifierClause(byte[] ski)
            : this(ski, true)
        {
        }

        internal X509SubjectKeyIdentifierClause(byte[] ski, bool cloneBuffer)
            : base(null, ski, cloneBuffer)
        {
        }

        private static byte[] GetSkiRawData(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            X509SubjectKeyIdentifierExtension skiExtension =
                certificate.Extensions[SubjectKeyIdentifierOid] as X509SubjectKeyIdentifierExtension;
            if (skiExtension != null)
            {
                return skiExtension.RawData;
            }
            else
            {
                return null;
            }
        }

        public byte[] GetX509SubjectKeyIdentifier()
        {
            return GetBuffer();
        }

        public bool Matches(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                return false;
            }

            byte[] data = GetSkiRawData(certificate);
            return data != null && Matches(data, SkiDataOffset);
        }

        public static bool TryCreateFrom(X509Certificate2 certificate, out X509SubjectKeyIdentifierClause keyIdentifierClause)
        {
            byte[] data = GetSkiRawData(certificate);
            keyIdentifierClause = null;
            if (data != null)
            {
                byte[] ski = SecurityUtils.CloneBuffer(data, SkiDataOffset, data.Length - SkiDataOffset);
                keyIdentifierClause = new X509SubjectKeyIdentifierClause(ski, false);
            }
            return keyIdentifierClause != null;
        }

        public static bool CanCreateFrom(X509Certificate2 certificate)
        {
            return null != GetSkiRawData(certificate);
        }


    }
}
