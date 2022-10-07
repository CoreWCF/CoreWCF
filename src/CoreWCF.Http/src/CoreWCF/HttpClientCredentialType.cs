// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public enum HttpClientCredentialType
    {
        None,
        Basic,
        Digest,
        Ntlm,
        Windows,
        Certificate,
        InheritedFromHost
    }

    internal static class HttpClientCredentialTypeHelper
    {
        internal static bool IsDefined(HttpClientCredentialType value)
        {
            return (value == HttpClientCredentialType.None ||
                value == HttpClientCredentialType.Basic ||
                value == HttpClientCredentialType.Digest ||
                value == HttpClientCredentialType.Ntlm ||
                value == HttpClientCredentialType.Windows ||
                value == HttpClientCredentialType.Certificate ||
                value == HttpClientCredentialType.InheritedFromHost);
        }

        internal static AuthenticationSchemes MapToAuthenticationScheme(HttpClientCredentialType clientCredentialType)
        {
            AuthenticationSchemes result;
            switch (clientCredentialType)
            {
                case HttpClientCredentialType.Certificate:
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

        internal static HttpClientCredentialType MapToClientCredentialType(AuthenticationSchemes authenticationSchemes)
        {
            HttpClientCredentialType result;
            switch (authenticationSchemes)
            {
                case AuthenticationSchemes.Anonymous:
                    result = HttpClientCredentialType.None;
                    break;
                case AuthenticationSchemes.Basic:
                    result = HttpClientCredentialType.Basic;
                    break;
                case AuthenticationSchemes.Digest:
                    result = HttpClientCredentialType.Digest;
                    break;
                case AuthenticationSchemes.Ntlm:
                    result = HttpClientCredentialType.Ntlm;
                    break;
                case AuthenticationSchemes.Negotiate:
                    result = HttpClientCredentialType.Windows;
                    break;
                default:
                    Fx.Assert("unsupported client AuthenticationScheme");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
            return result;
        }
    }
}
