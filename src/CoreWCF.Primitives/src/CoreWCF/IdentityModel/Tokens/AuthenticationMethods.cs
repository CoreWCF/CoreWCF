// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Defines constants for SAML authentication methods.
    /// </summary>
    public static class AuthenticationMethods
    {
        public const string Namespace = "http://schemas.microsoft.com/ws/2008/06/identity/authenticationmethod/";
        public const string HardwareToken           = Namespace + "hardwaretoken";
        public const string Kerberos                = Namespace + "kerberos";
        public const string Password                = Namespace + "password";
        public const string Pgp                     = Namespace + "pgp";
        public const string SecureRemotePassword    = Namespace + "secureremotepassword";
        public const string Signature               = Namespace + "signature";
        public const string Smartcard               = Namespace + "smartcard";
        public const string SmartcardPki            = Namespace + "smartcardpki";
        public const string Spki                    = Namespace + "spki";
        public const string TlsClient               = Namespace + "tlsclient";
        public const string Unspecified             = Namespace + "unspecified";
        public const string Windows                 = Namespace + "windows";
        public const string Xkms                    = Namespace + "xkms";
        public const string X509                    = Namespace + "x509";
    }
}
