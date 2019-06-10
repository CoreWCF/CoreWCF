using CoreWCF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class RsaSecurityToken : SecurityToken
    {
        string id;
        DateTime effectiveTime;
        RSA rsa;

        public RsaSecurityToken(RSA rsa)
            : this(rsa, SecurityUniqueId.Create().Value)
        {
        }

        public RsaSecurityToken(RSA rsa, string id)
        {
            if (rsa == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rsa));
            if (id == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            this.rsa = rsa;
            this.id = id;
            effectiveTime = DateTime.UtcNow;
        }

        public override string Id
        {
            get { return id; }
        }

        public override DateTime ValidFrom
        {
            get { return effectiveTime; }
        }

        public override DateTime ValidTo
        {
            // Never expire
            get { return SecurityUtils.MaxUtcDateTime; }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get
            {
                throw new PlatformNotSupportedException("RsaSecurityKey");
            }
        }

        public RSA Rsa
        {
            get { return rsa; }
        }
    }
}
