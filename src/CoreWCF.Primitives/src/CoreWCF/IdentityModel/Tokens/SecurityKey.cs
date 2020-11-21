using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    public abstract class SecurityKey
    {
        public abstract int KeySize { get; }
        public abstract byte[] DecryptKey(string algorithm, byte[] keyData);
        public abstract byte[] EncryptKey(string algorithm, byte[] keyData);
       // public abstract bool IsAsymmetricAlgorithm(string algorithm);
        public abstract bool IsSupportedAlgorithm(string algorithm);
       // public abstract bool IsSymmetricAlgorithm(string algorithm);
    }
}
