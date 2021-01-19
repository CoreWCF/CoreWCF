using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace CoreWCF.IdentityModel.Tokens
{
    public abstract class AsymmetricSecurityKey : SecurityKey
    {
        public abstract AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool privateKey);
        public abstract HashAlgorithm GetHashAlgorithmForSignature(string algorithm);
        public abstract AsymmetricSignatureDeformatter GetSignatureDeformatter(string algorithm);
        public abstract AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm);
        public abstract bool HasPrivateKey();
    }
}
