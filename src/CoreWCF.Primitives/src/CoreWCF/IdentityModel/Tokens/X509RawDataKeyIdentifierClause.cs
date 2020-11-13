using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace CoreWCF.IdentityModel.Tokens
{
    public class X509RawDataKeyIdentifierClause : BinaryKeyIdentifierClause
    {
        private X509Certificate2 _certificate;
        private X509AsymmetricSecurityKey _key;

        public X509RawDataKeyIdentifierClause(X509Certificate2 certificate)
            : this(GetRawData(certificate), false)
        {
            _certificate = certificate;
        }

        public X509RawDataKeyIdentifierClause(byte[] certificateRawData)
            : this(certificateRawData, true)
        {
        }

        internal X509RawDataKeyIdentifierClause(byte[] certificateRawData, bool cloneBuffer)
            : base(null, certificateRawData, cloneBuffer)
        {
        }

        public override bool CanCreateKey
        {
            get { return true; }
        }

        public override SecurityKey CreateKey()
        {
            if (_key == null)
            {
                if (_certificate == null)
                {
                    _certificate = new X509Certificate2(GetBuffer());
                }

                _key = new X509AsymmetricSecurityKey(_certificate);
            }
            return _key;
        }

        private static byte[] GetRawData(X509Certificate certificate)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            return certificate.GetRawCertData();
        }

        public byte[] GetX509RawData()
        {
            return GetBuffer();
        }

        public bool Matches(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                return false;
            }

            return Matches(GetRawData(certificate));
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "X509RawDataKeyIdentifierClause(RawData = {0})", ToBase64String());
        }
    }
}
