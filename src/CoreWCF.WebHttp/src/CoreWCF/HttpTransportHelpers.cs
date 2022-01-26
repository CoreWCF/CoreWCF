// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal static class HttpTransportHelpers
    {
        private const string DefaultRealm = HttpTransportDefaults.Realm;

        internal static void ConfigureTransportProtectionAndAuthentication(HttpsTransportBindingElement https, HttpTransportSecurity transportSecurity)
        {
            ConfigureAuthentication(https, transportSecurity);
            https.RequireClientCertificate = (transportSecurity.ClientCredentialType == HttpClientCredentialType.Certificate);
        }

        internal static void ConfigureTransportAuthentication(HttpTransportBindingElement http, HttpTransportSecurity transportSecurity)
        {
            if (transportSecurity.ClientCredentialType == HttpClientCredentialType.Certificate)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.CertificateUnsupportedForHttpTransportCredentialOnly));
            }

            ConfigureAuthentication(http, transportSecurity);
        }

        internal static void DisableTransportAuthentication(HttpTransportBindingElement http)
        {
            DisableAuthentication(http);
        }

        private static void ConfigureAuthentication(HttpTransportBindingElement http, HttpTransportSecurity transportSecurity)
        {
            http.AuthenticationScheme = MapToAuthenticationScheme(transportSecurity.ClientCredentialType);
            http.Realm = transportSecurity.Realm;
            http.ExtendedProtectionPolicy = transportSecurity.ExtendedProtectionPolicy;
        }

        private static AuthenticationSchemes MapToAuthenticationScheme(HttpClientCredentialType clientCredentialType)
        {
            AuthenticationSchemes result;
            switch (clientCredentialType)
            {
                case HttpClientCredentialType.Certificate:
                // fall through to None case
                case HttpClientCredentialType.None:
                    result = AuthenticationSchemes.Anonymous;
                    break;
                case HttpClientCredentialType.Basic:
                    result = AuthenticationSchemes.Basic;
                    break;
                case HttpClientCredentialType.Digest:
                    result = AuthenticationSchemes.Digest;
                    break;
                case HttpClientCredentialType.Ntlm:
                    result = AuthenticationSchemes.Ntlm;
                    break;
                case HttpClientCredentialType.Windows:
                    result = AuthenticationSchemes.Negotiate;
                    break;
                case HttpClientCredentialType.InheritedFromHost:
                    result = AuthenticationSchemes.None;
                    break;
                default:
                    Fx.Assert("unsupported client credential type");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
            return result;
        }

        private static void DisableAuthentication(HttpTransportBindingElement http)
        {
            http.AuthenticationScheme = AuthenticationSchemes.Anonymous;
            http.Realm = DefaultRealm;
            //ExtendedProtectionPolicy is always copied - even for security mode None, Message and TransportWithMessageCredential,
            //because the settings for ExtendedProtectionPolicy are always below the <security><transport> element
            //http.ExtendedProtectionPolicy = extendedProtectionPolicy;
        }
    }
}
