// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
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
            {
                return 1;
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(SslProtocols)));
            }
        }
    }

    internal class ServiceModelDictionaryManager
    {
        private static DictionaryManager s_dictionaryManager;

        public static DictionaryManager Instance
        {
            get
            {
                if (s_dictionaryManager == null)
                {
                    s_dictionaryManager = new DictionaryManager((ServiceModelDictionary)BinaryMessageEncoderFactory.XmlDictionary);
                }

                return s_dictionaryManager;
            }
        }
    }

    internal class SecurityUtils
    {
        public const string Principal = "Principal";
        public const string Identities = "Identities";
        public const string AuthTypeCertMap = "SSL/PCT";
        private static SecurityIdentifier s_administratorsSid;
        internal static byte[] ReadContentAsBase64(XmlDictionaryReader reader, long maxBufferSize)
        {
            throw new PlatformNotSupportedException();
        }

        private static bool s_computedDomain;

        internal static byte[] EncryptKey(SecurityToken wrappingToken, string wrappingAlgorithm, byte[] keyToWrap)
        {
            throw new PlatformNotSupportedException();
        }

        private static string s_currentDomain;
        private static IIdentity s_anonymousIdentity;
        private static X509SecurityTokenAuthenticator s_nonValidatingX509Authenticator;
        private static byte[] s_combinedHashLabel;

        internal static byte[] CombinedHashLabel
        {
            get
            {
                if (s_combinedHashLabel == null)
                    s_combinedHashLabel = Encoding.UTF8.GetBytes(TrustApr2004Strings.CombinedHashLabel);
                return s_combinedHashLabel;
            }
        }

        internal static IIdentity AnonymousIdentity
        {
            get
            {
                if (s_anonymousIdentity == null)
                {
                    s_anonymousIdentity = CreateIdentity(string.Empty);
                }
                return s_anonymousIdentity;
            }
        }

        internal static X509SecurityTokenAuthenticator NonValidatingX509Authenticator
        {
            get
            {
                if (s_nonValidatingX509Authenticator == null)
                {
                    s_nonValidatingX509Authenticator = new X509SecurityTokenAuthenticator(X509CertificateValidator.None);
                }
                return s_nonValidatingX509Authenticator;
            }
        }

        public static DateTime MinUtcDateTime => new DateTime(DateTime.MinValue.Ticks + TimeSpan.TicksPerDay, DateTimeKind.Utc);

        public static DateTime MaxUtcDateTime =>
                // + and -  TimeSpan.TicksPerDay is to compensate the DateTime.ParseExact (to localtime) overflow.
                new DateTime(DateTime.MaxValue.Ticks - TimeSpan.TicksPerDay, DateTimeKind.Utc);

        public static SecurityIdentifier AdministratorsSid
        {
            get
            {
                if (s_administratorsSid == null)
                {
                    s_administratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                }

                return s_administratorsSid;
            }
        }

    

        internal static ReadOnlyCollection<SecurityKey> CreateSymmetricSecurityKeys(byte[] key)
        {
            List<SecurityKey> temp = new List<SecurityKey>(1)
            {
                new InMemorySymmetricSecurityKey(key)
            };
            return temp.AsReadOnly();
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
                return CreateWindowsIdentity();
            }
        }

        internal static string GetSpnFromIdentity(EndpointIdentity identity, EndpointAddress target)
        {
            bool foundSpn = false;
            string spn = null;
            if (identity != null)
            {
                if (ClaimTypes.Spn.Equals(identity.IdentityClaim.ClaimType))
                {
                    spn = (string)identity.IdentityClaim.Resource;
                    foundSpn = true;
                }
                else if (ClaimTypes.Upn.Equals(identity.IdentityClaim.ClaimType))
                {
                    spn = (string)identity.IdentityClaim.Resource;
                    foundSpn = true;
                }
                else if (ClaimTypes.Dns.Equals(identity.IdentityClaim.ClaimType))
                {
                    spn = string.Format(CultureInfo.InvariantCulture, "host/{0}", (string)identity.IdentityClaim.Resource);
                    foundSpn = true;
                }
            }
            if (!foundSpn)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.CannotDetermineSPNBasedOnAddress, target)));
            }
            return spn;
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
                    identity = EndpointIdentity.CreateSpnIdentity(string.Format(CultureInfo.InvariantCulture, "host/{0}", DnsCache.MachineName));
                }
                else
                {
                    // Save windowsIdentity for delay lookup
                    identity = new UpnEndpointIdentity(CloneWindowsIdentityIfNecessary(self));
                }
            }

            return identity;
        }

        internal static int GetMaxNegotiationBufferSize(BindingContext bindingContext)
        {
            TransportBindingElement transport = bindingContext.RemainingBindingElements.Find<TransportBindingElement>();
            Fx.Assert(transport != null, "TransportBindingElement is null!");
            int maxNegoMessageSize;
            //TODO move below binding elements to Primitives
            //if (transport is ConnectionOrientedTransportBindingElement)
            //{
            //    maxNegoMessageSize = ((ConnectionOrientedTransportBindingElement)transport).MaxBufferSize;
            //}
            //else if (transport is HttpTransportBindingElement)
            //{
            //    maxNegoMessageSize = ((HttpTransportBindingElement)transport).MaxBufferSize;
            //}
            //else
            //{
                maxNegoMessageSize = TransportDefaults.MaxBufferSize;
           // }
            return maxNegoMessageSize;
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
            if (s_computedDomain == false)
            {
                try
                {
                    if (isSystemAccount)
                    {
                        s_currentDomain = Domain.GetComputerDomain().Name;
                    }
                    else
                    {
                        s_currentDomain = Domain.GetCurrentDomain().Name;
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
                    s_computedDomain = true;
                }
            }
            return s_currentDomain;
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

        public static WrappedKeySecurityToken CreateTokenFromEncryptedKeyClause(EncryptedKeyIdentifierClause keyClause, SecurityToken unwrappingToken)
        {
            SecurityKeyIdentifier wrappingTokenReference = keyClause.EncryptingKeyIdentifier;
            byte[] wrappedKey = keyClause.GetEncryptedKey();
            SecurityKey unwrappingSecurityKey = unwrappingToken.SecurityKeys[0];
            string wrappingAlgorithm = keyClause.EncryptionMethod;
            byte[] unwrappedKey = unwrappingSecurityKey.DecryptKey(wrappingAlgorithm, wrappedKey);
            //TODO, check value for XmlDictionaryString Symmetric or else 
            return new WrappedKeySecurityToken(SecurityUtils.GenerateId(), unwrappedKey, wrappingAlgorithm,
               XmlDictionaryString.Empty, unwrappingToken, wrappingTokenReference, wrappedKey, unwrappingSecurityKey
                    );
        }

        private static bool CanKeyDoKeyExchange(X509Certificate2 certificate)
        {
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
            bool canDoKeyExchange = (keyUsageExtension.KeyUsages &
                (X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment)) != X509KeyUsageFlags.None;
            return canDoKeyExchange;
        }

        internal static byte[] DecryptKey(SecurityToken unwrappingToken, string encryptionMethod, byte[] wrappedKey, out SecurityKey unwrappingSecurityKey)
        {
            unwrappingSecurityKey = null;
            if (unwrappingToken.SecurityKeys != null)
            {
                for (int i = 0; i < unwrappingToken.SecurityKeys.Count; ++i)
                {
                    if (unwrappingToken.SecurityKeys[i].IsSupportedAlgorithm(encryptionMethod))
                    {
                        unwrappingSecurityKey = unwrappingToken.SecurityKeys[i];
                        break;
                    }
                }
            }
            if (unwrappingSecurityKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.CannotFindMatchingCrypto, encryptionMethod)));
            }
            return unwrappingSecurityKey.DecryptKey(encryptionMethod, wrappedKey);
        }

        internal static MessageFault CreateSecurityContextNotFoundFault(SecurityStandardsManager standardsManager, string action)
        {
            SecureConversationDriver scDriver = standardsManager.SecureConversationDriver;
            FaultCode subCode = new FaultCode(scDriver.BadContextTokenFaultCode.Value, scDriver.Namespace.Value);
            FaultReason reason;
            if (action != null)
            {
                reason = new FaultReason(SR.Format(SR.BadContextTokenOrActionFaultReason, action), CultureInfo.CurrentCulture);
            }
            else
            {
                reason = new FaultReason(SR.Format(SR.BadContextTokenFaultReason), CultureInfo.CurrentCulture);
            }
            FaultCode senderCode = FaultCode.CreateSenderFaultCode(subCode);
            return MessageFault.CreateFault(senderCode, reason);
        }

        internal static MessageFault CreateSecurityMessageFault(Exception e, SecurityStandardsManager standardsManager)
        {
            bool isSecurityError = false;
            bool isTokenValidationError = false;
            bool isGenericTokenError = false;
            FaultException faultException = null;
            while (e != null)
            {
                if (e is SecurityTokenValidationException)
                {
                    if (e is SecurityContextTokenValidationException)
                    {
                        return CreateSecurityContextNotFoundFault(SecurityStandardsManager.DefaultInstance, null);
                    }
                    isSecurityError = true;
                    isTokenValidationError = true;
                    break;
                }
                else if (e is SecurityTokenException)
                {
                    isSecurityError = true;
                    isGenericTokenError = true;
                    break;
                }
                else if (e is MessageSecurityException ms)
                {
                    if (ms.Fault != null)
                    {
                        return ms.Fault;
                    }
                    isSecurityError = true;
                }
                else if (e is FaultException fe)
                {
                    faultException = fe;
                    break;
                }
                e = e.InnerException;
            }
            if (!isSecurityError && faultException == null)
            {
                return null;
            }
            FaultCode subCode;
            FaultReason reason;
            SecurityVersion wss = standardsManager.SecurityVersion;
            if (isTokenValidationError)
            {
                subCode = new FaultCode(wss.FailedAuthenticationFaultCode.Value, wss.HeaderNamespace.Value);
                reason = new FaultReason(SR.Format(SR.FailedAuthenticationFaultReason), CultureInfo.CurrentCulture);
            }
            else if (isGenericTokenError)
            {
                subCode = new FaultCode(wss.InvalidSecurityFaultCode.Value, wss.HeaderNamespace.Value);
                reason = new FaultReason(SR.Format(SR.InvalidSecurityTokenFaultReason), CultureInfo.CurrentCulture);
            }
            else if (faultException != null)
            {
                // Only support Code and Reason.  No detail or action customization.
                return MessageFault.CreateFault(faultException.Code, faultException.Reason);
            }
            else
            {
                subCode = new FaultCode(wss.InvalidSecurityFaultCode.Value, wss.HeaderNamespace.Value);
                reason = new FaultReason(SR.Format(SR.InvalidSecurityFaultReason), CultureInfo.CurrentCulture);
            }
            FaultCode senderCode = FaultCode.CreateSenderFaultCode(subCode);
            return MessageFault.CreateFault(senderCode, reason);
        }

        internal static string GenerateId() => SecurityUniqueId.Create().Value;

        internal static byte[] GenerateDerivedKey(SecurityToken tokenToDerive, string derivationAlgorithm, byte[] label, byte[] nonce, int keySize, int offset)
        {
            SymmetricSecurityKey symmetricSecurityKey = GetSecurityKey<SymmetricSecurityKey>(tokenToDerive);
            if (symmetricSecurityKey == null || !symmetricSecurityKey.IsSupportedAlgorithm(derivationAlgorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.CannotFindMatchingCrypto, derivationAlgorithm)));
            }
            return symmetricSecurityKey.GenerateDerivedKey(derivationAlgorithm, label, nonce, keySize, offset);
        }

        public static bool TryCreateKeyFromIntrinsicKeyClause(SecurityKeyIdentifierClause keyIdentifierClause, SecurityTokenResolver resolver, out SecurityKey key)
        {
            key = null;
            if (keyIdentifierClause.CanCreateKey)
            {
                key = keyIdentifierClause.CreateKey();
                return true;
            }
            if (keyIdentifierClause is EncryptedKeyIdentifierClause keyClause)
            {
                for (int i = 0; i < keyClause.EncryptingKeyIdentifier.Count; i++)
                {
                    if (resolver.TryResolveSecurityKey(keyClause.EncryptingKeyIdentifier[i], out SecurityKey unwrappingSecurityKey))
                    {
                        byte[] wrappedKey = keyClause.GetEncryptedKey();
                        string wrappingAlgorithm = keyClause.EncryptionMethod;
                        byte[] unwrappedKey = unwrappingSecurityKey.DecryptKey(wrappingAlgorithm, wrappedKey);
                        key = new InMemorySymmetricSecurityKey(unwrappedKey, false);
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool HasSymmetricSecurityKey(SecurityToken sourceEncryptionToken)
        {
            return GetSecurityKey<SymmetricSecurityKey>(sourceEncryptionToken) != null;
        }

        internal static byte[] CloneBuffer(byte[] buffer)
        {
            byte[] copy = Fx.AllocateByteArray(buffer.Length);
            Buffer.BlockCopy(buffer, 0, copy, 0, buffer.Length);
            return copy;
        }

        internal static byte[] CloneBuffer(byte[] buffer, int offset, int len)
        {
            DiagnosticUtility.DebugAssert(offset >= 0, "Negative offset passed to CloneBuffer.");
            DiagnosticUtility.DebugAssert(len >= 0, "Negative len passed to CloneBuffer.");
            DiagnosticUtility.DebugAssert(buffer.Length - offset >= len, "Invalid parameters to CloneBuffer.");

            byte[] copy = Fx.AllocateByteArray(len);
            Buffer.BlockCopy(buffer, offset, copy, 0, len);
            return copy;
        }

        internal static bool IsSupportedAlgorithm(string algorithm, SecurityToken token)
        {
            if (token.SecurityKeys == null)
            {
                return false;
            }
            for (int i = 0; i < token.SecurityKeys.Count; ++i)
            {
                if (token.SecurityKeys[i].IsSupportedAlgorithm(algorithm))
                {
                    return true;
                }
            }
            return false;
        }
        internal static T GetSecurityKey<T>(SecurityToken token) where T : SecurityKey
        {
            T result = null;
            if (token.SecurityKeys != null)
            {
                for (int i = 0; i < token.SecurityKeys.Count; ++i)
                {
                    T temp = (token.SecurityKeys[i] as T);
                    if (temp != null)
                    {
                        if (result != null)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.MultipleMatchingCryptosFound, typeof(T))));
                        }
                        else
                        {
                            result = temp;
                        }
                    }
                }
            }
            return result;
        }

        internal static bool TryCreateX509CertificateFromRawData(byte[] rawData, out X509Certificate2 certificate)
        {
            certificate = (rawData == null || rawData.Length == 0) ? null : new X509Certificate2(rawData);
            return certificate != null && certificate.Handle != IntPtr.Zero;
        }

        internal static string GetKeyDerivationAlgorithm(SecureConversationVersion version)
        {
            string derivationAlgorithm;
            if (version == SecureConversationVersion.WSSecureConversationFeb2005)
            {
                derivationAlgorithm = SecurityAlgorithms.Psha1KeyDerivation;
            }
            else if (version == SecureConversationVersion.WSSecureConversation13)
            {
                derivationAlgorithm = SecurityAlgorithms.Psha1KeyDerivationDec2005;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            return derivationAlgorithm;
        }

        internal static IIdentity CreateIdentity(string name, string authenticationType)
        {
            return new GenericIdentity(name, authenticationType);
        }

        internal static ReadOnlyCollection<IAuthorizationPolicy> CloneAuthorizationPoliciesIfNecessary(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationPolicies != null && authorizationPolicies.Count > 0)
            {
                bool clone = false;
                for (int i = 0; i < authorizationPolicies.Count; ++i)
                {
                    if (authorizationPolicies[i] is UnconditionalPolicy policy && policy.IsDisposable)
                    {
                        clone = true;
                        break;
                    }
                }
                if (clone)
                {
                    List<IAuthorizationPolicy> ret = new List<IAuthorizationPolicy>(authorizationPolicies.Count);
                    for (int i = 0; i < authorizationPolicies.Count; ++i)
                    {
                        if (authorizationPolicies[i] is UnconditionalPolicy policy)
                        {
                            ret.Add(policy.Clone());
                        }
                        else
                        {
                            ret.Add(authorizationPolicies[i]);
                        }
                    }
                    return ret.AsReadOnly();
                }
            }
            return authorizationPolicies;
        }

        public static void DisposeAuthorizationPoliciesIfNecessary(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationPolicies != null && authorizationPolicies.Count > 0)
            {
                for (int i = 0; i < authorizationPolicies.Count; ++i)
                {
                    DisposeIfNecessary(authorizationPolicies[i] as UnconditionalPolicy);
                }
            }
        }

        public static void DisposeIfNecessary(IDisposable obj)
        {
            if (obj != null)
            {
                obj.Dispose();
            }
        }

        public static ChannelBinding GetChannelBindingFromMessage(Message message)
        {
            if (message == null)
            {
                return null;
            }

            ChannelBindingMessageProperty.TryGet(message, out ChannelBindingMessageProperty channelBindingMessageProperty);
            ChannelBinding channelBinding = null;

            if (channelBindingMessageProperty != null)
            {
                channelBinding = channelBindingMessageProperty.ChannelBinding;
            }

            return channelBinding;
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

        private static class NetworkCredentialHelper
        {
            internal static bool IsNullOrEmpty(NetworkCredential credential)
            {
                return credential == null ||
                        (
                            string.IsNullOrEmpty(UnsafeGetUsername(credential)) &&
                            string.IsNullOrEmpty(UnsafeGetDomain(credential)) &&
                            string.IsNullOrEmpty(UnsafeGetPassword(credential))
                        );
            }

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

        internal static X509Certificate2 GetCertificateFromStore(StoreName storeName, StoreLocation storeLocation,
            X509FindType findType, object findValue, EndpointAddress target)
        {
            X509Certificate2 certificate = GetCertificateFromStoreCore(storeName, storeLocation, findType, findValue, target, true);
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.CannotFindCert, storeName, storeLocation, findType, findValue)));
            }

            return certificate;
        }

        private static X509Certificate2 GetCertificateFromStoreCore(StoreName storeName, StoreLocation storeLocation,
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
                ResetAllCertificates(certs);
                store.Close();
            }
        }

        private static Exception CreateCertificateLoadException(StoreName storeName, StoreLocation storeLocation,
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

        internal static UniqueId GenerateUniqueId()
        {
            return new UniqueId();
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

        internal static void ErasePasswordInUsernameTokenIfPresent(SecurityMessageProperty bootstrapMessageProperty)
        {
            throw new NotImplementedException();
        }

        internal static void ResetCertificate(X509Certificate2 certificate)
        {
            certificate.Reset();
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

        public static void ValidateAnonymityConstraint(WindowsIdentity identity, bool allowUnauthenticatedCallers)
        {
            if (!allowUnauthenticatedCallers && identity.User.IsWellKnown(WellKnownSidType.AnonymousSid))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(
                    new SecurityTokenValidationException(SR.AnonymousLogonsAreNotAllowed));
            }
        }
        internal static ReadOnlyCollection<IAuthorizationPolicy> CreatePrincipalNameAuthorizationPolicies(string principalName)
        {
            if (principalName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(principalName));
            }

            Claim identityClaim;
            Claim primaryPrincipal;
            if (principalName.Contains("@") || principalName.Contains(@"\"))
            {
                identityClaim = new Claim(ClaimTypes.Upn, principalName, Rights.Identity);
                primaryPrincipal = Claim.CreateUpnClaim(principalName);
            }
            else
            {
                identityClaim = new Claim(ClaimTypes.Spn, principalName, Rights.Identity);
                primaryPrincipal = Claim.CreateSpnClaim(principalName);
            }

            List<Claim> claims = new List<Claim>(2)
            {
                identityClaim,
                primaryPrincipal
            };

            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1)
            {
                new UnconditionalPolicy(CreateIdentity(principalName), new DefaultClaimSet(ClaimSet.Anonymous, claims))
            };
            return policies.AsReadOnly();
        }

        public static SecurityBindingElement GetIssuerSecurityBindingElement(ServiceModelSecurityTokenRequirement requirement)
        {
            SecurityBindingElement bindingElement = requirement.SecureConversationSecurityBindingElement;
            if (bindingElement != null)
            {
                return bindingElement;
            }

            Binding binding = requirement.IssuerBinding;
            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.IssuerBindingNotPresentInTokenRequirement, requirement));
            }
            BindingElementCollection bindingElements = binding.CreateBindingElements();
            return bindingElements.Find<SecurityBindingElement>();
        }

        internal static SecurityStandardsManager CreateSecurityStandardsManager(MessageSecurityVersion securityVersion, SecurityTokenManager tokenManager)
        {
            SecurityTokenSerializer tokenSerializer = tokenManager.CreateSecurityTokenSerializer(securityVersion.SecurityTokenVersion);
            return new SecurityStandardsManager(securityVersion, tokenSerializer);
        }

        internal static SecurityStandardsManager CreateSecurityStandardsManager(SecurityTokenRequirement requirement, SecurityTokenManager tokenManager)
        {
            MessageSecurityTokenVersion securityVersion = (MessageSecurityTokenVersion)requirement.GetProperty<MessageSecurityTokenVersion>(ServiceModelSecurityTokenRequirement.MessageSecurityVersionProperty);
            if (securityVersion == MessageSecurityTokenVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10)
            {
                return CreateSecurityStandardsManager(MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10, tokenManager);
            }
            else if (securityVersion == MessageSecurityTokenVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005)
            {
                return CreateSecurityStandardsManager(MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11, tokenManager);
            }
            else if (securityVersion == MessageSecurityTokenVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10)
            {
                return CreateSecurityStandardsManager(MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10, tokenManager);
            }
            else if (securityVersion == MessageSecurityTokenVersion.WSSecurity10WSTrust13WSSecureConversation13BasicSecurityProfile10)
            {
                return CreateSecurityStandardsManager(MessageSecurityVersion.WSSecurity10WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10, tokenManager);
            }
            else if (securityVersion == MessageSecurityTokenVersion.WSSecurity11WSTrust13WSSecureConversation13)
            {
                return CreateSecurityStandardsManager(MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12, tokenManager);
            }
            else if (securityVersion == MessageSecurityTokenVersion.WSSecurity11WSTrust13WSSecureConversation13BasicSecurityProfile10)
            {
                return CreateSecurityStandardsManager(MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10, tokenManager);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        internal static SecurityStandardsManager CreateSecurityStandardsManager(MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
        {
            if (messageSecurityVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(messageSecurityVersion)));
            }
            if (securityTokenSerializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityTokenSerializer));
            }
            return new SecurityStandardsManager(messageSecurityVersion, securityTokenSerializer);
        }

        internal static void MatchRstWithEndpointFilter(Message request, IMessageFilterTable<EndpointAddress> endpointFilterTable, Uri listenUri)
        {
            if (endpointFilterTable == null)
            {
                return;
            }
            Collection<EndpointAddress> result = new Collection<EndpointAddress>();
            if (!endpointFilterTable.GetMatchingValues(request, result))
            {
                throw new SecurityNegotiationException(SR.Format(SR.RequestSecurityTokenDoesNotMatchEndpointFilters, listenUri));
            }
        }

        internal static bool IsEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool IsCurrentlyTimeEffective(DateTime effectiveTime, DateTime expirationTime, TimeSpan maxClockSkew)
        {
            DateTime curEffectiveTime = (effectiveTime < DateTime.MinValue.Add(maxClockSkew)) ? effectiveTime : effectiveTime.Subtract(maxClockSkew);
            DateTime curExpirationTime = (expirationTime > DateTime.MaxValue.Subtract(maxClockSkew)) ? expirationTime : expirationTime.Add(maxClockSkew);
            DateTime curTime = DateTime.UtcNow;

            return (curEffectiveTime.ToUniversalTime() <= curTime) && (curTime < curExpirationTime.ToUniversalTime());
        }

        // match the RST with the endpoint filters in case there is at least 1 asymmetric signature in the message
        internal static bool ShouldMatchRstWithEndpointFilter(SecurityBindingElement sbe)
        {
            foreach (SecurityTokenParameters parameters in new SecurityTokenParametersEnumerable(sbe, true))
            {
                if (parameters.HasAsymmetricKey)
                {
                    return true;
                }
            }
            return false;
        }

        internal static string GetIdentityNamesFromPolicies(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            return GetIdentityNamesFromContext(AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies));
        }

        internal static string GetIdentityNamesFromContext(AuthorizationContext authContext)
        {
            if (authContext == null)
                return string.Empty;

            StringBuilder str = new StringBuilder(256);
            for (int i = 0; i < authContext.ClaimSets.Count; ++i)
            {
                ClaimSet claimSet = authContext.ClaimSets[i];

                // Windows
                if (claimSet is WindowsClaimSet windows)
                {
                    if (str.Length > 0)
                        str.Append(", ");

                    AppendIdentityName(str, windows.WindowsIdentity);
                }
                else
                {
                    // X509
                    if (claimSet is X509CertificateClaimSet x509)
                    {
                        if (str.Length > 0)
                            str.Append(", ");

                        AppendCertificateIdentityName(str, x509.X509Certificate);
                    }
                }
            }

            if (str.Length <= 0)
            {
                List<IIdentity> identities = null;
                if (authContext.Properties.TryGetValue(SecurityUtils.Identities, out object obj))
                {
                    identities = obj as List<IIdentity>;
                }
                if (identities != null)
                {
                    for (int i = 0; i < identities.Count; ++i)
                    {
                        IIdentity identity = identities[i];
                        if (identity != null)
                        {
                            if (str.Length > 0)
                                str.Append(", ");

                            AppendIdentityName(str, identity);
                        }
                    }
                }
            }
            return str.Length <= 0 ? string.Empty : str.ToString();
        }

        internal static void AppendIdentityName(StringBuilder str, IIdentity identity)
        {
            string name = null;
            try
            {
                name = identity.Name;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                // suppress exception, this is just info.
            }

            str.Append(string.IsNullOrEmpty(name) ? "<null>" : name);

            if (identity is WindowsIdentity windows)
            {
                if (windows.User != null)
                {
                    str.Append("; ");
                    str.Append(windows.User.ToString());
                }
            }
            else
            {
                if (identity is WindowsSidIdentity sid)
                {
                    str.Append("; ");
                    str.Append(sid.SecurityIdentifier.ToString());
                }
            }
        }

        internal static void AppendCertificateIdentityName(StringBuilder str, X509Certificate2 certificate)
        {
            string value = certificate.SubjectName.Name;
            if (string.IsNullOrEmpty(value))
            {
                value = certificate.GetNameInfo(X509NameType.DnsName, false);
                if (string.IsNullOrEmpty(value))
                {
                    value = certificate.GetNameInfo(X509NameType.SimpleName, false);
                    if (string.IsNullOrEmpty(value))
                    {
                        value = certificate.GetNameInfo(X509NameType.EmailName, false);
                        if (string.IsNullOrEmpty(value))
                        {
                            value = certificate.GetNameInfo(X509NameType.UpnName, false);
                        }
                    }
                }
            }
            // Same format as X509Identity
            str.Append(string.IsNullOrEmpty(value) ? "<x509>" : value);
            str.Append("; ");
            str.Append(certificate.Thumbprint);
        }

        internal static string GetCertificateId(X509Certificate2 certificate)
        {
            string certificateId = certificate.SubjectName.Name;
            if (string.IsNullOrEmpty(certificateId))
            {
                certificateId = certificate.Thumbprint;
            }

            return certificateId;
        }

        internal static bool MatchesBuffer(byte[] src, byte[] dst)
        {
            return MatchesBuffer(src, 0, dst, 0);
        }

        internal static bool MatchesBuffer(byte[] src, int srcOffset, byte[] dst, int dstOffset)
        {
            DiagnosticUtility.DebugAssert(dstOffset >= 0, "Negative dstOffset passed to MatchesBuffer.");
            DiagnosticUtility.DebugAssert(srcOffset >= 0, "Negative srcOffset passed to MatchesBuffer.");

            // defensive programming
            if ((dstOffset < 0) || (srcOffset < 0))
            {
                return false;
            }

            if (src == null || srcOffset >= src.Length)
            {
                return false;
            }

            if (dst == null || dstOffset >= dst.Length)
            {
                return false;
            }

            if ((src.Length - srcOffset) != (dst.Length - dstOffset))
            {
                return false;
            }

            for (int i = srcOffset, j = dstOffset; i < src.Length; i++, j++)
            {
                if (src[i] != dst[j])
                {
                    return false;
                }
            }
            return true;
        }

        internal static string ClaimSetToString(ClaimSet claimSet)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ClaimSet [");
            for (int i = 0; i < claimSet.Count; i++)
            {
                Claim claim = claimSet[i];
                if (claim != null)
                {
                    sb.Append("  ");
                    sb.AppendLine(claim.ToString());
                }
            }
            string prefix = "] by ";
            ClaimSet issuer = claimSet;
            do
            {
                issuer = issuer.Issuer;
                sb.AppendFormat("{0}{1}", prefix, issuer == claimSet ? "Self" : (issuer.Count <= 0 ? "Unknown" : issuer[0].ToString()));
                prefix = " -> ";
            } while (issuer.Issuer != issuer);
            return sb.ToString();
        }

        internal static ReadOnlyCollection<IAuthorizationPolicy> CreateAuthorizationPolicies(ClaimSet claimSet)
        {
            return CreateAuthorizationPolicies(claimSet, MaxUtcDateTime);
        }

        internal static ReadOnlyCollection<IAuthorizationPolicy> CreateAuthorizationPolicies(ClaimSet claimSet, DateTime expirationTime)
        {
            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1)
            {
                new UnconditionalPolicy(claimSet, expirationTime)
            };
            return policies.AsReadOnly();
        }

        internal static IIdentity CloneIdentityIfNecessary(IIdentity identity)
        {
            if (identity != null)
            {
                if (identity is WindowsIdentity wid)
                {
                    return CloneWindowsIdentityIfNecessary(wid);
                }
            }
            return identity;
        }

        internal static ClaimSet CloneClaimSetIfNecessary(ClaimSet claimSet)
        {
            if (claimSet != null)
            {
                if (claimSet is WindowsClaimSet wic)
                {
                    return wic.Clone();
                }
            }
            return claimSet;
        }

        internal static ReadOnlyCollection<ClaimSet> CloneClaimSetsIfNecessary(ReadOnlyCollection<ClaimSet> claimSets)
        {
            if (claimSets != null)
            {
                bool clone = false;
                for (int i = 0; i < claimSets.Count; ++i)
                {
                    if (claimSets[i] is WindowsClaimSet)
                    {
                        clone = true;
                        break;
                    }
                }
                if (clone)
                {
                    List<ClaimSet> ret = new List<ClaimSet>(claimSets.Count);
                    for (int i = 0; i < claimSets.Count; ++i)
                    {
                        ret.Add(CloneClaimSetIfNecessary(claimSets[i]));
                    }
                    return ret.AsReadOnly();
                }
            }
            return claimSets;
        }

        internal static void DisposeClaimSetIfNecessary(ClaimSet claimSet)
        {
            if (claimSet != null)
            {
                DisposeIfNecessary(claimSet as WindowsClaimSet);
            }
        }

        internal static void DisposeClaimSetsIfNecessary(ReadOnlyCollection<ClaimSet> claimSets)
        {
            if (claimSets != null)
            {
                for (int i = 0; i < claimSets.Count; ++i)
                {
                    DisposeIfNecessary(claimSets[i] as WindowsClaimSet);
                }
            }
        }

        private class SimpleAuthorizationContext : AuthorizationContext
        {
            private SecurityUniqueId _id;
            private readonly UnconditionalPolicy _policy;
            private readonly IDictionary<string, object> _properties;

            public SimpleAuthorizationContext(IList<IAuthorizationPolicy> authorizationPolicies)
            {
                _policy = (UnconditionalPolicy)authorizationPolicies[0];
                Dictionary<string, object> properties = new Dictionary<string, object>();
                if (_policy.PrimaryIdentity != null && _policy.PrimaryIdentity != AnonymousIdentity)
                {
                    List<IIdentity> identities = new List<IIdentity>
                    {
                        _policy.PrimaryIdentity
                    };
                    properties.Add(Identities, identities);
                }
                // Might need to port ReadOnlyDictionary?
                _properties = properties;
            }

            public override string Id
            {
                get
                {
                    if (_id == null)
                    {
                        _id = SecurityUniqueId.Create();
                    }

                    return _id.Value;
                }
            }
            public override ReadOnlyCollection<ClaimSet> ClaimSets { get { return _policy.Issuances; } }
            public override DateTime ExpirationTime { get { return _policy.ExpirationTime; } }
            public override IDictionary<string, object> Properties { get { return _properties; } }
        }
        internal static AuthorizationContext CreateDefaultAuthorizationContext(IList<IAuthorizationPolicy> authorizationPolicies)
        {
            AuthorizationContext authorizationContext;
            // This is faster than Policy evaluation.
            if (authorizationPolicies != null && authorizationPolicies.Count == 1 && authorizationPolicies[0] is UnconditionalPolicy)
            {
                authorizationContext = new SimpleAuthorizationContext(authorizationPolicies);
            }
            // degenerate case
            else if (authorizationPolicies == null || authorizationPolicies.Count <= 0)
            {
                return DefaultAuthorizationContext.Empty;
            }
            else
            {
                // there are some policies, run them until they are all done
                DefaultEvaluationContext evaluationContext = new DefaultEvaluationContext();
                object[] policyState = new object[authorizationPolicies.Count];
                object done = new object();

                int oldContextCount;
                do
                {
                    oldContextCount = evaluationContext.Generation;

                    for (int i = 0; i < authorizationPolicies.Count; i++)
                    {
                        if (policyState[i] == done)
                        {
                            continue;
                        }

                        IAuthorizationPolicy policy = authorizationPolicies[i];
                        if (policy == null)
                        {
                            policyState[i] = done;
                            continue;
                        }

                        if (policy.Evaluate(evaluationContext, ref policyState[i]))
                        {
                            policyState[i] = done;

                            /* if (DiagnosticUtility.ShouldTraceVerbose)
                             {
                                 TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.AuthorizationPolicyEvaluated,
                                     SR.GetString(SR.AuthorizationPolicyEvaluated, policy.Id));
                             }*/
                        }
                    }
                } while (oldContextCount < evaluationContext.Generation);

                authorizationContext = new DefaultAuthorizationContext(evaluationContext);
            }

            /*  if (DiagnosticUtility.ShouldTraceInformation)
              {
                  TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.AuthorizationContextCreated,
                      SR.GetString(SR.AuthorizationContextCreated, authorizationContext.Id));
              }*/

            return authorizationContext;
        }
        public static bool IsRequestSecurityContextIssuance(string actionString)
        {
            if (string.CompareOrdinal(actionString, XD.SecureConversationFeb2005Dictionary
                .RequestSecurityContextIssuance.Value) == 0 ||
                string.CompareOrdinal(actionString, XD.SecureConversationApr2004Dictionary
                .RequestSecurityContextIssuance.Value) == 0)
            {
                return true;
            }

            return false;
        }
    }

    internal static class EmptyReadOnlyCollection<T>
    {
        public static ReadOnlyCollection<T> Instance = new ReadOnlyCollection<T>(new List<T>());
    }
}
