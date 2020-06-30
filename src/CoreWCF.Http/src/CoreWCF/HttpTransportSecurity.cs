using System;
using System.ComponentModel;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF.Channels;
using CoreWCF.Security;
using System.Net;
using System.Net.Security;

namespace CoreWCF
{
    public sealed class HttpTransportSecurity
    {
        internal const HttpClientCredentialType DefaultClientCredentialType = HttpClientCredentialType.None;
        internal const string DefaultRealm = CoreWCF.Channels.HttpTransportDefaults.Realm;

        HttpClientCredentialType clientCredentialType;
        string realm;
        ExtendedProtectionPolicy extendedProtectionPolicy;

        public HttpTransportSecurity()
        {
            clientCredentialType = DefaultClientCredentialType;
            realm = DefaultRealm;
            extendedProtectionPolicy = ChannelBindingUtility.DefaultPolicy;
        }

        public HttpClientCredentialType ClientCredentialType
        {
            get { return clientCredentialType; }
            set
            {
                if (!HttpClientCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                clientCredentialType = value;
            }
        }

        public string Realm
        {
            get { return realm; }
            set { realm = value; }
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy
        {
            get
            {
                return extendedProtectionPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.PolicyEnforcement == PolicyEnforcement.Always &&
                    !System.Security.Authentication.ExtendedProtection.ExtendedProtectionPolicy.OSSupportsExtendedProtection)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new PlatformNotSupportedException(SR.ExtendedProtectionNotSupported));
                }

                extendedProtectionPolicy = value;
            }
        }

        internal void ConfigureTransportProtectionOnly(HttpsTransportBindingElement https)
        {
            DisableAuthentication(https);
            https.RequireClientCertificate = false;
        }

        void ConfigureAuthentication(HttpTransportBindingElement http)
        {
            http.AuthenticationScheme = HttpClientCredentialTypeHelper.MapToAuthenticationScheme(clientCredentialType);
            http.Realm = Realm;
            //http.ExtendedProtectionPolicy = extendedProtectionPolicy;
        }

        static void ConfigureAuthentication(HttpTransportBindingElement http, HttpTransportSecurity transportSecurity)
        {
            transportSecurity.clientCredentialType = HttpClientCredentialTypeHelper.MapToClientCredentialType(http.AuthenticationScheme);
            // transportSecurity.pro = httpproxycredentialtypehelper.maptoproxycredentialtype(http.proxyauthenticationscheme);
            transportSecurity.realm = http.Realm;
            transportSecurity.extendedProtectionPolicy = http.ExtendedProtectionPolicy;
        }

        void DisableAuthentication(HttpTransportBindingElement http)
        {
            http.AuthenticationScheme = AuthenticationSchemes.Anonymous;
            http.Realm = DefaultRealm;
            //ExtendedProtectionPolicy is always copied - even for security mode None, Message and TransportWithMessageCredential,
            //because the settings for ExtendedProtectionPolicy are always below the <security><transport> element
            //http.ExtendedProtectionPolicy = extendedProtectionPolicy;
        }

        //static bool IsDisabledAuthentication(HttpTransportBindingElement http)
        //{
        //    return http.AuthenticationScheme == AuthenticationSchemes.Anonymous && http.ProxyAuthenticationScheme == AuthenticationSchemes.Anonymous && http.Realm == DefaultRealm;
        //}

        internal void ConfigureTransportProtectionAndAuthentication(HttpsTransportBindingElement https)
        {
            ConfigureAuthentication(https);
            https.RequireClientCertificate = (clientCredentialType == HttpClientCredentialType.Certificate);
        }

        internal static void ConfigureTransportProtectionAndAuthentication(HttpsTransportBindingElement https, HttpTransportSecurity transportSecurity)
        {
            ConfigureAuthentication(https, transportSecurity);
            if (https.RequireClientCertificate)
                transportSecurity.ClientCredentialType = HttpClientCredentialType.Certificate;
        }

        internal void ConfigureTransportAuthentication(HttpTransportBindingElement http)
        {
            if (clientCredentialType == HttpClientCredentialType.Certificate)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.CertificateUnsupportedForHttpTransportCredentialOnly));
            }

            ConfigureAuthentication(http);
        }

        //internal static bool IsConfiguredTransportAuthentication(HttpTransportBindingElement http, HttpTransportSecurity transportSecurity)
        //{
        //    if (HttpClientCredentialTypeHelper.MapToClientCredentialType(http.AuthenticationScheme) == HttpClientCredentialType.Certificate)
        //        return false;
        //    ConfigureAuthentication(http, transportSecurity);
        //    return true;
        //}

        internal void DisableTransportAuthentication(HttpTransportBindingElement http)
        {
            DisableAuthentication(http);
        }

        //internal static bool IsDisabledTransportAuthentication(HttpTransportBindingElement http)
        //{
        //    return IsDisabledAuthentication(http);
        //}
    }
}
