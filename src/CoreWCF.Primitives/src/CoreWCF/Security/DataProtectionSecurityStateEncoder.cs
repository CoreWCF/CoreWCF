// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Security
{
    public class DataProtectionSecurityStateEncoder : SecurityStateEncoder
    {
        private readonly Lazy<IDataProtector> _dataProtector;
        private readonly byte[] _entropy;
        private readonly string _purpose = typeof(DataProtectionSecurityStateEncoder).FullName;

        public DataProtectionSecurityStateEncoder() : this(true)
        { }

        public DataProtectionSecurityStateEncoder(bool useCurrentUserProtectionScope) : this(useCurrentUserProtectionScope, null)
        { }

        public DataProtectionSecurityStateEncoder(bool useCurrentUserProtectionScope, byte[] entropy)
        {
            if (entropy != null)
            {
                _entropy = entropy;
                _purpose = Encoding.UTF8.GetString(_entropy);
            }
            else
            {
                _entropy = Encoding.UTF8.GetBytes(_purpose);
            }

            _dataProtector = new Lazy<IDataProtector>(GetDataProtector);
        }

        public bool UseCurrentUserProtectionScope => false;

        public byte[] GetEntropy() => _entropy;

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.Append(GetType().ToString());
            result.AppendFormat("{0}  UseCurrentUserProtectionScope={1}", Environment.NewLine, UseCurrentUserProtectionScope);
            result.AppendFormat("{0}  Entropy Length={1}", Environment.NewLine, (_entropy == null) ? 0 : _entropy.Length);
            return result.ToString();
        }


        private IDataProtector GetDataProtector()
        {
            HttpContext context = null;
            if (OperationContext.Current.RequestContext.RequestMessage.Properties.TryGetValue("Microsoft.AspNetCore.Http.HttpContext", out object contextObj))
            {
                context = contextObj as HttpContext;
                return context?.RequestServices.GetDataProtector(_purpose);
            }

            return null;
        }


        protected internal override byte[] DecodeSecurityState(byte[] data)
        {
            try
            {
                return _dataProtector.Value.Unprotect(data);
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
                return _dataProtector.Value.Protect(data);
            }
            catch (CryptographicException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.SecurityStateEncoderEncodingFailure, exception));
            }
        }
    }
}
