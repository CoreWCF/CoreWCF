// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using System;
using Microsoft.AspNetCore.DataProtection;

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Provides cookie integrity and confidentiality using <see cref="ProtectedData"/>.
    /// </summary>
    /// <remarks>
    /// Due to the nature of <see cref="ProtectedData"/>, cookies
    /// which use this tranform can only be read by the same machine 
    /// which wrote them. As such, this transform is not appropriate
    /// for use in applications that run on a web server farm.
    /// </remarks>
    public sealed class ProtectedDataCookieTransform : CookieTransform
    {
        private const string EntropyString = "CoreWCF.IdentityModel.ProtectedDataCookieTransform";
        private IDataProtector _protector;

        public ProtectedDataCookieTransform(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(EntropyString);
        }
        /// <summary>
        /// Verifies data protection.
        /// </summary>
        /// <param name=nameof(encoded)>Data previously returned from <see cref="Encode"/></param>
        /// <returns>The originally protected data.</returns>
        /// <exception cref="ArgumentNullException">The argument 'encoded' is null.</exception>
        /// <exception cref="ArgumentException">The argument 'encoded' contains zero bytes.</exception>
        public override byte[] Decode(byte[] encoded)
        {
            if (null == encoded)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encoded));
            }

            if (0 == encoded.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(encoded), SR.Format(SR.ID6045));
            }

            // CurrentUser is used here, and this has been tested as 
            // NetworkService. Using CurrentMachine allows anyone on 
            // the machine to decrypt the data, which isn't what we 
            // want.
            byte[] decoded;
            try
            {
                decoded = _protector.Unprotect(encoded);
            }
            catch (CryptographicException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID1073), e));
            }

            return decoded;
        }

        /// <summary>
        /// Protects data.
        /// </summary>
        /// <param name=nameof(value)>Data to be protected.</param>
        /// <returns>Protected data.</returns>
        /// <exception cref="ArgumentNullException">The argument 'value' is null.</exception>
        /// <exception cref="ArgumentException">The argument 'value' contains zero bytes.</exception>
        public override byte[] Encode(byte[] value)
        {
            if (null == value)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }

            if (0 == value.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(value), SR.Format(SR.ID6044));
            }

            // See note in Decode about the DataProtectionScope.
            byte[] encoded;
            try
            {
                encoded = _protector.Protect(value);
            }
            catch (CryptographicException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID1074), e));
            }

            return encoded;
        }
    }
}
