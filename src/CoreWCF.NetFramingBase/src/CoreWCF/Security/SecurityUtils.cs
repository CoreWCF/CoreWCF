// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;

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
            return CreateWindowsIdentity(false);
        }

        internal static EndpointIdentity CreateWindowsIdentity(bool spnOnly)
        {
            EndpointIdentity identity = null;
            WindowsIdentity self = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsIdentity.GetCurrent() : null;
            using (self)
            {
                if (self is null || spnOnly || IsSystemAccount(self))
                {
                    // If we're running on a non-Windows platform, we can't use the current Windows identity.
                    // If we're running on Windows and the current identity is a system account, we also can't use it.
                    // In both cases, we create an SPN identity based on the machine name.
                    // This is used for Net.Tcp services that don't have a configured identity.
                    // The SPN will be used to authenticate the service to clients.
                    identity = new SpnEndpointIdentity(string.Format(CultureInfo.InvariantCulture, "host/{0}", DnsCache.MachineName));
                }
                else
                {
                    // This is used when generating the WSDL. It calls GetProperty<EndpointIdentity>
                    // on the configured binding and uses the identity in the WSDL. It's also used by
                    // SspiNegotiationTokenAuthenticator to calculate the DefaultServiceBinding value.
                    // On Linux, we cannot use the current Windows Identity, so we will return an Spn
                    // based on the machine name. It's quite a bit of work to get Windows authentication
                    // to work on Linux, so if a developer has gone to that kind of effort to use
                    // Windows authentication on Linux, they can provide an explicit endpoint identity
                    // in the service configuration (or manually set DefaultServiceBinding) and this
                    // code won't be needed.
                    var upn = GetUpnNameWithFallback(self.Name);
                    identity = new UpnEndpointIdentity(upn);
                }
            }

            return identity;
        }

        private static string GetUpnNameWithFallback(string downlevelName)
        {
            // There is no managed API to get the UPN name, so we have to P/Invoke. This will only work on Windows.
            // If we're not on Windows, we just return the downlevel name. If someone is using Windows auth on Linux
            // they should be using an explicit endpoint identity.
            if (Interop.Secur32.GetCurrentUpn(out string upnName))
            {
                return upnName;
            }

            // If the AD cannot be queried for the fully qualified domain name,
            // fall back to the downlevel UPN name
            return downlevelName;
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
