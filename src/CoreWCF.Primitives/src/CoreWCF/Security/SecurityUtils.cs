using System;
using System.Security.Authentication;
using System.ComponentModel;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Selectors;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using System.Security.Principal;
using System.Diagnostics;
using System.Net;
using CoreWCF.IdentityModel.Tokens;
using System.Globalization;
using CoreWCF.Channels;
using System.DirectoryServices.ActiveDirectory;
using CoreWCF.Runtime;
using System.Collections.Generic;

namespace CoreWCF.Security
{
    static class ProtectionLevelHelper
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
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
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("p", (int)p, typeof(ProtectionLevel)));
                    case ProtectionLevel.None:
                        return 2;
                    case ProtectionLevel.Sign:
                        return 3;
                    case ProtectionLevel.EncryptAndSign:
                        return 4;
                }
            }
            else
                return 1;
        }
    }

    static class SslProtocolsHelper
    {
        internal static bool IsDefined(SslProtocols value)
        {
            SslProtocols allValues = SslProtocols.None;
            foreach (var protocol in Enum.GetValues(typeof(SslProtocols)))
            {
                allValues |= (SslProtocols)protocol;
            }
            return (value & allValues) == value;
        }

        internal static void Validate(SslProtocols value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(SslProtocols)));
            }
        }
    }
    public class SecurityUtils
    {
        public const string Identities = "Identities";
        static bool computedDomain;
        static string currentDomain;
        static IIdentity anonymousIdentity;
        static X509SecurityTokenAuthenticator nonValidatingX509Authenticator;

        internal static IIdentity AnonymousIdentity
        {
            get
            {
                if (anonymousIdentity == null)
                {
                    anonymousIdentity = SecurityUtils.CreateIdentity(string.Empty);
                }
                return anonymousIdentity;
            }
        }

        internal static X509SecurityTokenAuthenticator NonValidatingX509Authenticator
        {
            get
            {
                if (nonValidatingX509Authenticator == null)
                {
                    nonValidatingX509Authenticator = new X509SecurityTokenAuthenticator(X509CertificateValidator.None);
                }
                return nonValidatingX509Authenticator;
            }
        }

        internal static IIdentity CreateIdentity(string name)
        {
            return new GenericIdentity(name);
        }

        internal static EndpointIdentity CreateWindowsIdentity()
        {
            return CreateWindowsIdentity(false);
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
                return EndpointIdentity.CreateUpnIdentity(upn);
            }
            else
            {
                return SecurityUtils.CreateWindowsIdentity();
            }
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
                    || self.User.Value.StartsWith("S-1-5-82", StringComparison.OrdinalIgnoreCase));
        }

        internal static EndpointIdentity CreateWindowsIdentity(bool spnOnly)
        {
            EndpointIdentity identity = null;
            using (WindowsIdentity self = WindowsIdentity.GetCurrent())
            {
                bool isSystemAccount = IsSystemAccount(self);
                if (spnOnly || isSystemAccount)
                {
                    identity = EndpointIdentity.CreateSpnIdentity(String.Format(CultureInfo.InvariantCulture, "host/{0}", DnsCache.MachineName));
                }
                else
                {
                    // Save windowsIdentity for delay lookup
                    identity = new UpnEndpointIdentity(CloneWindowsIdentityIfNecessary(self));
                }
            }

            return identity;
        }

        internal static WindowsIdentity CloneWindowsIdentityIfNecessary(WindowsIdentity wid)
        {
            return SecurityUtils.CloneWindowsIdentityIfNecessary(wid, null);
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

        static IntPtr UnsafeGetWindowsIdentityToken(WindowsIdentity wid)
        {
            return wid.Token;
        }

        static WindowsIdentity UnsafeCreateWindowsIdentityFromToken(IntPtr token, string authType)
        {
            if (authType != null)
                return new WindowsIdentity(token, authType);
            else
                return new WindowsIdentity(token);
        }

        internal static Claim GetPrimaryIdentityClaim(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            return GetPrimaryIdentityClaim(AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies));
        }

        internal static Claim GetPrimaryIdentityClaim(AuthorizationContext authContext)
        {
            if (authContext != null)
            {
                for (int i = 0; i < authContext.ClaimSets.Count; ++i)
                {
                    ClaimSet claimSet = authContext.ClaimSets[i];
                    foreach (Claim claim in claimSet.FindClaims(null, Rights.Identity))
                    {
                        return claim;
                    }
                }
            }
            return null;
        }

        internal static string GetPrimaryDomain()
        {
            using (WindowsIdentity wid = WindowsIdentity.GetCurrent())
            {
                return GetPrimaryDomain(IsSystemAccount(wid));
            }
        }

        internal static string GetPrimaryDomain(bool isSystemAccount)
        {
            if (computedDomain == false)
            {
                try
                {
                    if (isSystemAccount)
                    {
                        currentDomain = Domain.GetComputerDomain().Name;
                    }
                    else
                    {
                        currentDomain = Domain.GetCurrentDomain().Name;
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);
                }
                finally
                {
                    computedDomain = true;
                }
            }
            return currentDomain;
        }

        internal static void EnsureCertificateCanDoKeyExchange(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }
            bool canDoKeyExchange = false;
            Exception innerException = null;
            if (certificate.HasPrivateKey)
            {
                try
                {
                    canDoKeyExchange = CanKeyDoKeyExchange(certificate);
                }
                // exceptions can be due to ACLs on the key etc
                catch (System.Security.SecurityException e)
                {
                    innerException = e;
                }
                catch (CryptographicException e)
                {
                    innerException = e;
                }
            }
            if (!canDoKeyExchange)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SslCertMayNotDoKeyExchange, certificate.SubjectName.Name), innerException));
            }
        }

        static bool CanKeyDoKeyExchange(X509Certificate2 certificate)
        {
            bool canDoKeyExchange = false;

            X509KeyUsageExtension keyUsageExtension = null;
            for (int i = 0; i < certificate.Extensions.Count; i++)
            {
                keyUsageExtension = certificate.Extensions[i] as X509KeyUsageExtension;
                if (keyUsageExtension != null)
                {
                    break;
                }
            }

            // No KeyUsage extension means most usages are permitted including key exchange.
            // See RFC 5280 section 4.2.1.3 (Key Usage) for details. If the extension is non-critical
            // then it's non-enforcing and meant as an aid in choosing the best certificate when
            // there are multiple certificates to choose from. 
            if (keyUsageExtension == null || !keyUsageExtension.Critical)
            {
                return true;
            }

            // One of KeyAgreement, KeyEncipherment or DigitalSignature need to be allowed depending on the cipher
            // being used. See RFC 5246 section 7.4.6 for more details.
            // Additionally, according to msdn docs for PFXImportCertStore, the key specification is set to AT_KEYEXCHANGE
            // when the data encipherment usage is set.
            canDoKeyExchange = (keyUsageExtension.KeyUsages &
                (X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment)) != X509KeyUsageFlags.None;

            return canDoKeyExchange;
        }

        internal static NetworkCredential GetNetworkCredentialsCopy(NetworkCredential networkCredential)
        {
            NetworkCredential result;
            if (networkCredential != null && !NetworkCredentialHelper.IsDefault(networkCredential))
            {
                result = new NetworkCredential(NetworkCredentialHelper.UnsafeGetUsername(networkCredential), NetworkCredentialHelper.UnsafeGetPassword(networkCredential), NetworkCredentialHelper.UnsafeGetDomain(networkCredential));
            }
            else
            {
                result = networkCredential;
            }
            return result;
        }

        static class NetworkCredentialHelper
        {
            static internal bool IsNullOrEmpty(NetworkCredential credential)
            {
                return credential == null ||
                        (
                            String.IsNullOrEmpty(UnsafeGetUsername(credential)) &&
                            String.IsNullOrEmpty(UnsafeGetDomain(credential)) &&
                            String.IsNullOrEmpty(UnsafeGetPassword(credential))
                        );
            }

            static internal bool IsDefault(NetworkCredential credential)
            {
                return UnsafeGetDefaultNetworkCredentials().Equals(credential);
            }

            static internal string UnsafeGetUsername(NetworkCredential credential)
            {
                return credential.UserName;
            }

            static internal string UnsafeGetPassword(NetworkCredential credential)
            {
                return credential.Password;
            }

            static internal string UnsafeGetDomain(NetworkCredential credential)
            {
                return credential.Domain;
            }

            static NetworkCredential UnsafeGetDefaultNetworkCredentials()
            {
                return CredentialCache.DefaultNetworkCredentials;
            }
        }

        internal static X509Certificate2 GetCertificateFromStore(StoreName storeName, StoreLocation storeLocation,
            X509FindType findType, object findValue, EndpointAddress target)
        {
            X509Certificate2 certificate = GetCertificateFromStoreCore(storeName, storeLocation, findType, findValue, target, true);
            if (certificate == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.CannotFindCert, storeName, storeLocation, findType, findValue)));

            return certificate;
        }

        static X509Certificate2 GetCertificateFromStoreCore(StoreName storeName, StoreLocation storeLocation,
            X509FindType findType, object findValue, EndpointAddress target, bool throwIfMultipleOrNoMatch)
        {
            if (findValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(findValue));
            }
            X509Store store = new X509Store(storeName, storeLocation);
            X509Certificate2Collection certs = null;
            try
            {
                store.Open(OpenFlags.ReadOnly);
                certs = store.Certificates.Find(findType, findValue, false);
                if (certs.Count == 1)
                {
                    return new X509Certificate2(certs[0]);
                }
                if (throwIfMultipleOrNoMatch)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateCertificateLoadException(
                        storeName, storeLocation, findType, findValue, target, certs.Count));
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                SecurityUtils.ResetAllCertificates(certs);
                store.Close();
            }
        }

        static Exception CreateCertificateLoadException(StoreName storeName, StoreLocation storeLocation,
            X509FindType findType, object findValue, EndpointAddress target, int certCount)
        {
            if (certCount == 0)
            {
                if (target == null)
                {
                    return new InvalidOperationException(SR.Format(SR.CannotFindCert, storeName, storeLocation, findType, findValue));
                }
                else
                {
                    return new InvalidOperationException(SR.Format(SR.CannotFindCertForTarget, storeName, storeLocation, findType, findValue, target));
                }
            }
            else
            {
                if (target == null)
                {
                    return new InvalidOperationException(SR.Format(SR.FoundMultipleCerts, storeName, storeLocation, findType, findValue));
                }
                else
                {
                    return new InvalidOperationException(SR.Format(SR.FoundMultipleCertsForTarget, storeName, storeLocation, findType, findValue, target));
                }
            }
        }

        // This is the workaround, Since store.Certificates returns a full collection
        // of certs in store.  These are holding native resources.
        internal static void ResetAllCertificates(X509Certificate2Collection certificates)
        {
            if (certificates != null)
            {
                for (int i = 0; i < certificates.Count; ++i)
                {
                    ResetCertificate(certificates[i]);
                }
            }
        }

        internal static void ResetCertificate(X509Certificate2 certificate)
        {
            certificate.Reset();
        }

        static bool TryCreateIdentity(ClaimSet claimSet, string claimType, out EndpointIdentity identity)
        {
            identity = null;
            foreach (Claim claim in claimSet.FindClaims(claimType, null))
            {
                identity = EndpointIdentity.CreateIdentity(claim);
                return true;
            }
            return false;
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

        internal static Task OpenTokenAuthenticatorIfRequiredAsync(SecurityTokenAuthenticator tokenAuthenticator, CancellationToken token)
        {
            return OpenCommunicationObjectAsync(tokenAuthenticator as ICommunicationObject, token);
        }

        internal static Task CloseTokenAuthenticatorIfRequiredAsync(SecurityTokenAuthenticator tokenAuthenticator, CancellationToken token)
        {
            return CloseTokenAuthenticatorIfRequiredAsync(tokenAuthenticator, false, token);
        }

        internal static Task CloseTokenAuthenticatorIfRequiredAsync(SecurityTokenAuthenticator tokenAuthenticator, bool aborted, CancellationToken token)
        {
            return CloseCommunicationObjectAsync(tokenAuthenticator, aborted, token);
        }

        static Task OpenCommunicationObjectAsync(ICommunicationObject obj, CancellationToken token)
        {
            if (obj != null)
                return obj.OpenAsync(token);

            return Task.CompletedTask;
        }

        static Task CloseCommunicationObjectAsync(object obj, bool aborted, CancellationToken token)
        {
            if (obj != null)
            {
                ICommunicationObject co = obj as ICommunicationObject;
                if (co != null)
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
                else if (obj is IDisposable)
                {
                    ((IDisposable)obj).Dispose();
                }
            }

            return Task.CompletedTask;
        }

        internal static EndpointIdentity GetServiceCertificateIdentity(X509Certificate2 certificate)
        {
            using (X509CertificateClaimSet claimSet = new X509CertificateClaimSet(certificate))
            {
                EndpointIdentity identity;
                if (!TryCreateIdentity(claimSet, ClaimTypes.Dns, out identity))
                {
                    TryCreateIdentity(claimSet, ClaimTypes.Rsa, out identity);
                }
                return identity;
            }
        }

        public static void ValidateAnonymityConstraint(WindowsIdentity identity, bool allowUnauthenticatedCallers)
        {
            if (!allowUnauthenticatedCallers && identity.User.IsWellKnown(WellKnownSidType.AnonymousSid))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(
                    new SecurityTokenValidationException(SR.AnonymousLogonsAreNotAllowed));
            }
        }
    }
}