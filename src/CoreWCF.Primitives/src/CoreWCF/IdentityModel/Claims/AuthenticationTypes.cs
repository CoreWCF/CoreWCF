// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Claims
{
    /// <summary>
    /// Defines types for WindowsIdentity AuthenticationType
    /// </summary>
    public static class AuthenticationTypes
    {
        public const string Basic = "Basic";
        public const string Federation  = "Federation";
        public const string Kerberos    = "Kerberos";
        public const string Negotiate   = "Negotiate";
        public const string Password    = "Password";
        public const string Signature   = "Signature";
        public const string Windows     = "Windows";
        public const string X509        = "X509";
    }
}
