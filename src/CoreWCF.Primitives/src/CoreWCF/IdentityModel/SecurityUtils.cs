using System;
using System.Text;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.Runtime;
using CoreWCF;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using CoreWCF.IdentityModel.Policy;

namespace CoreWCF.IdentityModel
{
    internal static class SecurityUtils
    {
        public const string Identities = "Identities";
        static IIdentity anonymousIdentity;

        public const string AuthTypeCertMap = "SSL/PCT"; // mapped from a cert

        internal static IIdentity AnonymousIdentity
        {
            get
            {
                if (anonymousIdentity == null)
                    anonymousIdentity = SecurityUtils.CreateIdentity(string.Empty);
                return anonymousIdentity;
            }
        }

        public static DateTime MaxUtcDateTime
        {
            get
            {
                // + and -  TimeSpan.TicksPerDay is to compensate the DateTime.ParseExact (to localtime) overflow.
                return new DateTime(DateTime.MaxValue.Ticks - TimeSpan.TicksPerDay, DateTimeKind.Utc);
            }
        }

        public static DateTime MinUtcDateTime
        {
            get
            {
                // + and -  TimeSpan.TicksPerDay is to compensate the DateTime.ParseExact (to localtime) overflow.
                return new DateTime(DateTime.MinValue.Ticks + TimeSpan.TicksPerDay, DateTimeKind.Utc);
            }
        }

        internal static IIdentity CreateIdentity(string name, string authenticationType)
        {
            return new GenericIdentity(name, authenticationType);
        }

        internal static IIdentity CreateIdentity(string name)
        {
            return new GenericIdentity(name);
        }

        internal static byte[] CloneBuffer(byte[] buffer)
        {
            return CloneBuffer(buffer, 0, buffer.Length);
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

        internal static string GetCertificateId(X509Certificate2 certificate)
        {
            string certificateId = certificate.SubjectName.Name;
            if (string.IsNullOrEmpty(certificateId))
                certificateId = certificate.Thumbprint;
            return certificateId;
        }

        internal static void ResetCertificate(X509Certificate2 certificate)
        {
            // Check that Dispose() and Reset() do the same thing
            certificate.Dispose();
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

        internal static ReadOnlyCollection<IAuthorizationPolicy> CreateAuthorizationPolicies(ClaimSet claimSet)
        {
            return CreateAuthorizationPolicies(claimSet, SecurityUtils.MaxUtcDateTime);
        }

        internal static ReadOnlyCollection<IAuthorizationPolicy> CreateAuthorizationPolicies(ClaimSet claimSet, DateTime expirationTime)
        {
            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1);
            policies.Add(new UnconditionalPolicy(claimSet, expirationTime));
            return policies.AsReadOnly();
        }

        internal static IIdentity CloneIdentityIfNecessary(IIdentity identity)
        {
            if (identity != null)
            {
                WindowsIdentity wid = identity as WindowsIdentity;
                if (wid != null)
                {
                    return CloneWindowsIdentityIfNecessary(wid);
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

        internal static ClaimSet CloneClaimSetIfNecessary(ClaimSet claimSet)
        {
            if (claimSet != null)
            {
                WindowsClaimSet wic = claimSet as WindowsClaimSet;
                if (wic != null)
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
                        ret.Add(SecurityUtils.CloneClaimSetIfNecessary(claimSets[i]));
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
                SecurityUtils.DisposeIfNecessary(claimSet as WindowsClaimSet);
            }
        }

        internal static void DisposeClaimSetsIfNecessary(ReadOnlyCollection<ClaimSet> claimSets)
        {
            if (claimSets != null)
            {
                for (int i = 0; i < claimSets.Count; ++i)
                {
                    SecurityUtils.DisposeIfNecessary(claimSets[i] as WindowsClaimSet);
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

        class SimpleAuthorizationContext : AuthorizationContext
        {
            SecurityUniqueId id;
            UnconditionalPolicy policy;
            IDictionary<string, object> properties;

            public SimpleAuthorizationContext(IList<IAuthorizationPolicy> authorizationPolicies)
            {
                this.policy = (UnconditionalPolicy)authorizationPolicies[0];
                Dictionary<string, object> properties = new Dictionary<string, object>();
                if (this.policy.PrimaryIdentity != null && this.policy.PrimaryIdentity != SecurityUtils.AnonymousIdentity)
                {
                    List<IIdentity> identities = new List<IIdentity>();
                    identities.Add(this.policy.PrimaryIdentity);
                    properties.Add(SecurityUtils.Identities, identities);
                }
                // Might need to port ReadOnlyDictionary?
                this.properties = properties;
            }

            public override string Id
            {
                get
                {
                    if (this.id == null)
                        this.id = SecurityUniqueId.Create();
                    return this.id.Value;
                }
            }
            public override ReadOnlyCollection<ClaimSet> ClaimSets { get { return this.policy.Issuances; } }
            public override DateTime ExpirationTime { get { return this.policy.ExpirationTime; } }
            public override IDictionary<string, object> Properties { get { return this.properties; } }
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
                            continue;

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
    }

    static class EmptyReadOnlyCollection<T>
    {
        public static ReadOnlyCollection<T> Instance = new ReadOnlyCollection<T>(new List<T>());
    }
}