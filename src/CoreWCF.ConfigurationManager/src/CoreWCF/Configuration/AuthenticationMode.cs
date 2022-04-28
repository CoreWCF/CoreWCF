// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Configuration
{
    //TODO As Authentication Modes are added
    public enum AuthenticationMode
    {
        //AnonymousForCertificate,
        //AnonymousForSslNegotiated,
        CertificateOverTransport,
        //IssuedToken,
        IssuedTokenForCertificate,
        IssuedTokenForSslNegotiated,
        IssuedTokenOverTransport,
        //Kerberos,
        //KerberosOverTransport,
        //MutualCertificate,
        //MutualCertificateDuplex,
        //MutualSslNegotiated,
        SecureConversation,
        //SspiNegotiated,
        //UserNameForCertificate,
        //UserNameForSslNegotiated,
        UserNameOverTransport,
        SspiNegotiatedOverTransport
    }
}
