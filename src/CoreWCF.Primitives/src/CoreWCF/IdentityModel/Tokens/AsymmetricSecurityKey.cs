// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
