// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Threading;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Selectors;
using System.ComponentModel;
using System.Security.Authentication;
using System.Net.Security;
using System.Net;
using CoreWCF.Channels;
using System.Globalization;
using System.Security.Principal;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    internal static class SecurityUtils
    {
        internal static void ResetCertificate(X509Certificate2 certificate)
        {
            certificate.Reset();
        }

        internal static EndpointIdentity CreateWindowsIdentity(NetworkCredential serverCredential)
        {
            if (serverCredential != null && !NetworkCredentialHelper.IsDefault(serverCredential))
            {
                string upn;
                if (serverCredential.Domain != null && serverCredential.Domain.Length > 0)
                {
                    upn = serverCredential.UserName + "@" + serverCredential.Domain;
                }
                else
                {
                    upn = serverCredential.UserName;
                }
                return new UpnEndpointIdentity(upn);
            }
            else
            {
                return CreateWindowsIdentity();
            }
        }

        internal static EndpointIdentity CreateWindowsIdentity()
        {
            EndpointIdentity identity = null;
            using (WindowsIdentity self = WindowsIdentity.GetCurrent())
            {
                bool isSystemAccount = IsSystemAccount(self);
                if (isSystemAccount)
                {
                    identity = new SpnEndpointIdentity(string.Format(CultureInfo.InvariantCulture, "host/{0}", DnsCache.MachineName));
                }
                else
                {
                    // As far as I can tell, this is only used when generating the WSDL. It calls GetProperty<EndpointIdentity>
                    // on the configured binding and uses the identity in the WSDL. Currently the security upgrade in the WSDL is broken
                    // for Net.Tcp so this code path is currently dead. Once that is fixed, we'll end up hitting this problem.
                    // We need to decide how to handle this. On .NET Framework UpnEndpointIdentity uses some native Win32 apis
                    // to fetch the current users UPN. Options to fix this are:
                    // 1. Use System.DirectoryServices.AccountManagement.UserPrincipal to get the UPN. This didn't work me (@mconnew)
                    //    when testing it on Windows. I believe this is caused by being Azure AD joined and not traditional on premise
                    //    Domain joined.
                    // 2. Keep using the win32 api behind a runtime check for OS which only calls it on Windows.
                    // 3. Require an explicitly set identity. This could potentially be a breaking change as upgrading the CoreWCF
                    //    version could result in failing to provide the WSDL at all if not provided.
                    // On Linux, it's unlikely that Windows auth is being used as you don't have a concept of the current logged
                    // in Windows user. In that scenario, we can probably say you have to explicitly configure the identity when
                    // configuring your service which will skip the lookup.
                    throw new PlatformNotSupportedException();
                    
                    // Save windowsIdentity for delay lookup
                    //identity = new UpnEndpointIdentity(CloneWindowsIdentityIfNecessary(self));
                }
            }

            return identity;
        }

        private static bool IsSystemAccount(WindowsIdentity self)
        {
            SecurityIdentifier sid = self.User;
            if (sid == null)
            {
                return false;
            }
            // S-1-5-82 is the prefix for the sid that represents the identity that IIS 7.5 Apppool thread runs under.
            return (sid.IsWellKnown(WellKnownSidType.LocalSystemSid)
                    || sid.IsWellKnown(WellKnownSidType.NetworkServiceSid)
                    || sid.IsWellKnown(WellKnownSidType.LocalServiceSid)
                    || sid.Value.StartsWith("S-1-5-82", StringComparison.OrdinalIgnoreCase));
        }

        internal static WindowsIdentity CloneWindowsIdentityIfNecessary(WindowsIdentity wid)
        {
            return CloneWindowsIdentityIfNecessary(wid, null);
        }

        internal static WindowsIdentity CloneWindowsIdentityIfNecessary(WindowsIdentity wid, string authType)
        {
            if (wid != null)
            {
                IntPtr token = UnsafeGetWindowsIdentityToken(wid);
                if (token != IntPtr.Zero)
                {
                    return UnsafeCreateWindowsIdentityFromToken(token, authType);
                }
            }
            return wid;
        }

        private static IntPtr UnsafeGetWindowsIdentityToken(WindowsIdentity wid)
        {
            return wid.Token;
        }

        private static WindowsIdentity UnsafeCreateWindowsIdentityFromToken(IntPtr token, string authType)
        {
            if (authType != null)
            {
                return new WindowsIdentity(token, authType);
            }
            else
            {
                return new WindowsIdentity(token);
            }
        }

        internal static void FixNetworkCredential(ref NetworkCredential credential)
        {
            if (credential == null)
            {
                return;
            }
            string username = NetworkCredentialHelper.UnsafeGetUsername(credential);
            string domain = NetworkCredentialHelper.UnsafeGetDomain(credential);
            if (!string.IsNullOrEmpty(username) && string.IsNullOrEmpty(domain))
            {
                // do the splitting only if there is exactly 1 \ or exactly 1 @
                string[] partsWithSlashDelimiter = username.Split('\\');
                string[] partsWithAtDelimiter = username.Split('@');
                if (partsWithSlashDelimiter.Length == 2 && partsWithAtDelimiter.Length == 1)
                {
                    if (!string.IsNullOrEmpty(partsWithSlashDelimiter[0]) && !string.IsNullOrEmpty(partsWithSlashDelimiter[1]))
                    {
                        credential = new NetworkCredential(partsWithSlashDelimiter[1], NetworkCredentialHelper.UnsafeGetPassword(credential), partsWithSlashDelimiter[0]);
                    }
                }
                else if (partsWithSlashDelimiter.Length == 1 && partsWithAtDelimiter.Length == 2)
                {
                    if (!string.IsNullOrEmpty(partsWithAtDelimiter[0]) && !string.IsNullOrEmpty(partsWithAtDelimiter[1]))
                    {
                        credential = new NetworkCredential(partsWithAtDelimiter[0], NetworkCredentialHelper.UnsafeGetPassword(credential), partsWithAtDelimiter[1]);
                    }
                }
            }
        }

        internal static EndpointIdentity GetServiceCertificateIdentity(X509Certificate2 certificate)
        {
            using (X509CertificateClaimSet claimSet = new X509CertificateClaimSet(certificate))
            {
                if (!TryCreateIdentity(claimSet, ClaimTypes.Dns, out EndpointIdentity identity))
                {
                    TryCreateIdentity(claimSet, ClaimTypes.Rsa, out identity);
                }
                return identity;
            }
        }

        private static bool TryCreateIdentity(ClaimSet claimSet, string claimType, out EndpointIdentity identity)
        {
            identity = null;
            foreach (Claim claim in claimSet.FindClaims(claimType, null))
            {
                identity = EndpointIdentity.CreateIdentity(claim);
                return true;
            }
            return false;
        }

        internal static Task OpenTokenAuthenticatorIfRequiredAsync(SecurityTokenAuthenticator tokenAuthenticator, CancellationToken token)
        {
            return OpenCommunicationObjectAsync(tokenAuthenticator as ICommunicationObject, token);
        }

        internal static Task OpenTokenProviderIfRequiredAsync(SecurityTokenProvider tokenProvider, CancellationToken token)
        {
            return OpenCommunicationObjectAsync(tokenProvider as ICommunicationObject, token);
        }

        internal static Task CloseTokenProviderIfRequiredAsync(SecurityTokenProvider tokenProvider, CancellationToken token)
        {
            return CloseCommunicationObjectAsync(tokenProvider, false, token);
        }

        internal static void AbortTokenAuthenticatorIfRequired(SecurityTokenAuthenticator tokenAuthenticator)
        {
            CloseCommunicationObjectAsync(tokenAuthenticator, true, CancellationToken.None).GetAwaiter().GetResult();
        }

        internal static void AbortTokenProviderIfRequired(SecurityTokenProvider tokenProvider)
        {
            CloseCommunicationObjectAsync(tokenProvider, true, CancellationToken.None).GetAwaiter().GetResult();
        }

        internal static Task CloseTokenAuthenticatorIfRequiredAsync(SecurityTokenAuthenticator tokenAuthenticator, CancellationToken token)
        {
            return CloseTokenAuthenticatorIfRequiredAsync(tokenAuthenticator, false, token);
        }

        internal static Task CloseTokenAuthenticatorIfRequiredAsync(SecurityTokenAuthenticator tokenAuthenticator, bool aborted, CancellationToken token)
        {
            return CloseCommunicationObjectAsync(tokenAuthenticator, aborted, token);
        }

        private static Task OpenCommunicationObjectAsync(ICommunicationObject obj, CancellationToken token)
        {
            if (obj != null)
            {
                return obj.OpenAsync(token);
            }

            return Task.CompletedTask;
        }

        private static Task CloseCommunicationObjectAsync(object obj, bool aborted, CancellationToken token)
        {
            if (obj != null)
            {
                if (obj is ICommunicationObject co)
                {
                    if (aborted)
                    {
                        try
                        {
                            co.Abort();
                        }
                        catch (CommunicationException e)
                        {
                            DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                        }
                    }
                    else
                    {
                        return co.CloseAsync(token);
                    }
                }
                else if (obj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        public static void ValidateAnonymityConstraint(WindowsIdentity identity, bool allowUnauthenticatedCallers)
        {
            if (!allowUnauthenticatedCallers && identity.User.IsWellKnown(WellKnownSidType.AnonymousSid))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(
                    new SecurityTokenValidationException(SR.AnonymousLogonsAreNotAllowed));
            }
        }

        private static class NetworkCredentialHelper
        {
            //internal static bool IsNullOrEmpty(NetworkCredential credential)
            //{
            //    return credential == null ||
            //            (
            //                string.IsNullOrEmpty(UnsafeGetUsername(credential)) &&
            //                string.IsNullOrEmpty(UnsafeGetDomain(credential)) &&
            //                string.IsNullOrEmpty(UnsafeGetPassword(credential))
            //            );
            //}

            internal static bool IsDefault(NetworkCredential credential)
            {
                return UnsafeGetDefaultNetworkCredentials().Equals(credential);
            }

            internal static string UnsafeGetUsername(NetworkCredential credential)
            {
                return credential.UserName;
            }

            internal static string UnsafeGetPassword(NetworkCredential credential)
            {
                return credential.Password;
            }

            internal static string UnsafeGetDomain(NetworkCredential credential)
            {
                return credential.Domain;
            }

            private static NetworkCredential UnsafeGetDefaultNetworkCredentials()
            {
                return CredentialCache.DefaultNetworkCredentials;
            }
        }
    }

    internal static class SslProtocolsHelper
    {
        internal static bool IsDefined(SslProtocols value)
        {
            SslProtocols allValues = SslProtocols.None;
            foreach (object protocol in Enum.GetValues(typeof(SslProtocols)))
            {
                allValues |= (SslProtocols)protocol;
            }
            return (value & allValues) == value;
        }

        internal static void Validate(SslProtocols value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SslProtocols)));
            }
        }
    }

    internal static class ProtectionLevelHelper
    {
        internal static bool IsDefined(ProtectionLevel value)
        {
            return (value == ProtectionLevel.None
                || value == ProtectionLevel.Sign
                || value == ProtectionLevel.EncryptAndSign);
        }

        internal static void Validate(ProtectionLevel value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(ProtectionLevel)));
            }
        }

        internal static bool IsStronger(ProtectionLevel v1, ProtectionLevel v2)
        {
            return ((v1 == ProtectionLevel.EncryptAndSign && v2 != ProtectionLevel.EncryptAndSign)
                    || (v1 == ProtectionLevel.Sign && v2 == ProtectionLevel.None));
        }

        internal static bool IsStrongerOrEqual(ProtectionLevel v1, ProtectionLevel v2)
        {
            return (v1 == ProtectionLevel.EncryptAndSign
                    || (v1 == ProtectionLevel.Sign && v2 != ProtectionLevel.EncryptAndSign));
        }

        internal static ProtectionLevel Max(ProtectionLevel v1, ProtectionLevel v2)
        {
            return IsStronger(v1, v2) ? v1 : v2;
        }

        internal static int GetOrdinal(Nullable<ProtectionLevel> p)
        {
            if (p.HasValue)
            {
                switch ((ProtectionLevel)p)
                {
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(p), (int)p, typeof(ProtectionLevel)));
                    case ProtectionLevel.None:
                        return 2;
                    case ProtectionLevel.Sign:
                        return 3;
                    case ProtectionLevel.EncryptAndSign:
                        return 4;
                }
            }
            else
            {
                return 1;
            }
        }
    }
}
