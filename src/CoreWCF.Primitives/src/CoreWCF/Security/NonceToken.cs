using System.Collections;
using CoreWCF;
using System.IO;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using System.Security.Cryptography;
using CoreWCF.Security.Tokens;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel;

namespace CoreWCF.Security
{


    sealed class NonceToken : BinarySecretSecurityToken
    {
        public NonceToken(byte[] key)
            : this(SecurityUniqueId.Create().Value, key)
        {
        }

        public NonceToken(string id, byte[] key)
            : base(id, key, false)
        {
        }

        public NonceToken(int keySizeInBits)
            : base(SecurityUniqueId.Create().Value, keySizeInBits, false)
        {
        }
    }
}
