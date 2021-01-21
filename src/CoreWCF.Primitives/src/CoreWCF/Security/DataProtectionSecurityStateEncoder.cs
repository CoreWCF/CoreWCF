// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{

    public class DataProtectionSecurityStateEncoder : SecurityStateEncoder
    {
        private readonly byte[] entropy;

        public DataProtectionSecurityStateEncoder() : this(true)
        {
            // empty
        }

        public DataProtectionSecurityStateEncoder(bool useCurrentUserProtectionScope) : this(useCurrentUserProtectionScope, null)
        { }

        public DataProtectionSecurityStateEncoder(bool useCurrentUserProtectionScope, byte[] entropy)
        {
            UseCurrentUserProtectionScope = useCurrentUserProtectionScope;
            if (entropy == null)
            {
                this.entropy = null;
            }
            else
            {
                this.entropy = Fx.AllocateByteArray(entropy.Length);
                Buffer.BlockCopy(entropy, 0, this.entropy, 0, entropy.Length);
            }
        }

        public bool UseCurrentUserProtectionScope { get; }

        public byte[] GetEntropy()
        {
            byte[] result = null;
            if (entropy != null)
            {
                result = Fx.AllocateByteArray(entropy.Length);
                Buffer.BlockCopy(entropy, 0, result, 0, entropy.Length);
            }
            return result;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.Append(GetType().ToString());
            result.AppendFormat("{0}  UseCurrentUserProtectionScope={1}", Environment.NewLine, UseCurrentUserProtectionScope);
            result.AppendFormat("{0}  Entropy Length={1}", Environment.NewLine, (entropy == null) ? 0 : entropy.Length);
            return result.ToString();
        }

        protected internal override byte[] DecodeSecurityState(byte[] data)
        {
            try
            {
                return ProtectedData.Unprotect(data, entropy, (UseCurrentUserProtectionScope) ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine);
            }
            catch (CryptographicException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.SecurityStateEncoderDecodingFailure, exception));
            }

        }

        protected internal override byte[] EncodeSecurityState(byte[] data)
        {
            try
            {
                return ProtectedData.Protect(data, entropy, (UseCurrentUserProtectionScope) ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine);
            }
            catch (CryptographicException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.SecurityStateEncoderEncodingFailure, exception));
            }
        }
    }
}
