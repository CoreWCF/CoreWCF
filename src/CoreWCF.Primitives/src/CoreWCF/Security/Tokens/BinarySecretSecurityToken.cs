// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class BinarySecretSecurityToken : SecurityToken
    {
        private readonly string _id;
        private readonly DateTime _effectiveTime;
        private readonly byte[] _key;
        private readonly ReadOnlyCollection<SecurityKey> _securityKeys;

        public BinarySecretSecurityToken(int keySizeInBits)
            : this(SecurityUniqueId.Create().Value, keySizeInBits)
        {
        }

        public BinarySecretSecurityToken(string id, int keySizeInBits)
            : this(id, keySizeInBits, true)
        {
        }

        public BinarySecretSecurityToken(byte[] key)
            : this(SecurityUniqueId.Create().Value, key)
        {
        }

        public BinarySecretSecurityToken(string id, byte[] key)
            : this(id, key, true)
        {
        }

        protected BinarySecretSecurityToken(string id, int keySizeInBits, bool allowCrypto)
        {
            throw new NotImplementedException();
        }

        protected BinarySecretSecurityToken(string id, byte[] key, bool allowCrypto)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            }

            _id = id;
            _effectiveTime = DateTime.UtcNow;
            _key = new byte[key.Length];
            Buffer.BlockCopy(key, 0, _key, 0, key.Length);
            if (allowCrypto)
            {
                _securityKeys = SecurityUtils.CreateSymmetricSecurityKeys(_key);
            }
            else
            {
                _securityKeys = EmptyReadOnlyCollection<SecurityKey>.Instance;
            }
        }

        public override string Id
        {
            get { return _id; }
        }

        public override DateTime ValidFrom
        {
            get { return _effectiveTime; }
        }

        public override DateTime ValidTo
        {
            // Never expire
            get { return DateTime.MaxValue; }
        }

        public int KeySize
        {
            get { return (_key.Length * 8); }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get { return _securityKeys; }
        }

        public byte[] GetKeyBytes()
        {
            return SecurityUtils.CloneBuffer(_key);
        }
    }
}
