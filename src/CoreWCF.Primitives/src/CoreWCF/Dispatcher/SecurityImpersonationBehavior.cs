// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Threading;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace CoreWCF.Dispatcher
{
    internal sealed class SecurityImpersonationBehavior
    {
        private readonly PrincipalPermissionMode principalPermissionMode;
        private readonly object roleProvider;
        private readonly bool impersonateCallerForAllOperations;
        private readonly Dictionary<string, string> ncNameMap;

        //Dictionary<string, string> domainNameMap;
        private Random random;
        private const int maxDomainNameMapSize = 5;
        private static WindowsPrincipal anonymousWindowsPrincipal;
        private static string s_directoryServerName = null;

        //AuditLevel auditLevel = ServiceSecurityAuditBehavior.defaultMessageAuthenticationAuditLevel;
        //AuditLogLocation auditLogLocation = ServiceSecurityAuditBehavior.defaultAuditLogLocation;
        //bool suppressAuditFailure = ServiceSecurityAuditBehavior.defaultSuppressAuditFailure;

        private SecurityImpersonationBehavior(DispatchRuntime dispatch)
        {
            principalPermissionMode = dispatch.PrincipalPermissionMode;
            impersonateCallerForAllOperations = dispatch.ImpersonateCallerForAllOperations;
            //this.auditLevel = dispatch.MessageAuthenticationAuditLevel;
            //this.auditLogLocation = dispatch.SecurityAuditLogLocation;
            //this.suppressAuditFailure = dispatch.SuppressAuditFailure;
            ncNameMap = new Dictionary<string, string>(maxDomainNameMapSize, StringComparer.OrdinalIgnoreCase);
        }

        public static SecurityImpersonationBehavior CreateIfNecessary(DispatchRuntime dispatch)
        {
            if (IsSecurityBehaviorNeeded(dispatch))
            {
                return new SecurityImpersonationBehavior(dispatch);
            }
            else
            {
                return null;
            }
        }

        private static WindowsPrincipal AnonymousWindowsPrincipal
        {
            get
            {
                if (anonymousWindowsPrincipal == null)
                {
                    anonymousWindowsPrincipal = new WindowsPrincipal(WindowsIdentity.GetAnonymous());
                }

                return anonymousWindowsPrincipal;
            }
        }

        private static bool IsSecurityBehaviorNeeded(DispatchRuntime dispatch)
        {
            if (dispatch.PrincipalPermissionMode != PrincipalPermissionMode.None)
            {
                return true;
            }

            // Impersonation behavior is required if 
            // 1) Contract requires it or 
            // 2) Contract allows it and config requires it
            for (int i = 0; i < dispatch.Operations.Count; i++)
            {
                DispatchOperation operation = dispatch.Operations[i];

                if (operation.Impersonation == ImpersonationOption.Required)
                {
                    return true;
                }
                else if (operation.Impersonation == ImpersonationOption.NotAllowed)
                {
                    // a validation rule enforces that config cannot require impersonation in this case
                    return false;
                }
            }

            // contract allows impersonation. Return true if config requires it.
            return dispatch.ImpersonateCallerForAllOperations;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private IPrincipal SetCurrentThreadPrincipal(ServiceSecurityContext securityContext, out bool isThreadPrincipalSet)
        {
            IPrincipal result = null;
            IPrincipal principal = null;

            ClaimsPrincipal claimsPrincipal = OperationContext.Current.ClaimsPrincipal;

            if (principalPermissionMode == PrincipalPermissionMode.UseWindowsGroups)
            {
                if (claimsPrincipal is WindowsPrincipal)
                {
                    principal = claimsPrincipal;
                }
                else if (securityContext.PrimaryIdentity != null && securityContext.PrimaryIdentity is GenericIdentity)
                {
                    principal = new ClaimsPrincipal(securityContext.PrimaryIdentity);
                }
                else
                {
                    principal = GetWindowsPrincipal(securityContext);
                }
            }
            else if (principalPermissionMode == PrincipalPermissionMode.Custom)
            {
                principal = GetCustomPrincipal(securityContext);
            }
            else if (principalPermissionMode == PrincipalPermissionMode.Always)
            {
                principal = claimsPrincipal ?? new ClaimsPrincipal(new ClaimsIdentity());
            }

            if (principal != null)
            {
                result = Thread.CurrentPrincipal;
                Thread.CurrentPrincipal = principal;
                isThreadPrincipalSet = true;
            }
            else
            {
                isThreadPrincipalSet = false;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IPrincipal GetCustomPrincipal(ServiceSecurityContext securityContext)
        {
            if (securityContext.AuthorizationContext.Properties.TryGetValue(SecurityUtils.Principal, out object customPrincipal) && customPrincipal is IPrincipal)
            {
                return (IPrincipal)customPrincipal;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.NoPrincipalSpecifiedInAuthorizationContext));
            }
        }

        internal bool IsSecurityContextImpersonationRequired(MessageRpc rpc)
        {
            return ((rpc.Operation.Impersonation == ImpersonationOption.Required)
                || ((rpc.Operation.Impersonation == ImpersonationOption.Allowed) && impersonateCallerForAllOperations));
        }

        internal bool IsImpersonationEnabledOnCurrentOperation(MessageRpc rpc)
        {
            return IsSecurityContextImpersonationRequired(rpc) ||
                    principalPermissionMode != PrincipalPermissionMode.None;
        }

        public T RunImpersonated<T>(MessageRpc rpc, Func<T> func)
        {
            T returnValue = default(T);
            IPrincipal originalPrincipal = null;
            bool isThreadPrincipalSet = false;
            ServiceSecurityContext securityContext;
            bool setThreadPrincipal = principalPermissionMode != PrincipalPermissionMode.None;
            bool isSecurityContextImpersonationOn = IsSecurityContextImpersonationRequired(rpc);
            if (setThreadPrincipal || isSecurityContextImpersonationOn)
            {
                securityContext = GetAndCacheSecurityContext(rpc);
            }
            else
            {
                securityContext = null;
            }

            if (setThreadPrincipal && securityContext != null)
            {
                originalPrincipal = SetCurrentThreadPrincipal(securityContext, out isThreadPrincipalSet);
            }

            try
            {
                if (isSecurityContextImpersonationOn)
                {
                    returnValue = RunImpersonated2(rpc, securityContext, isSecurityContextImpersonationOn, func);
                }
                else
                {
                    returnValue = func();
                }
            }
            finally
            {
                if (isThreadPrincipalSet)
                {
                    Thread.CurrentPrincipal = originalPrincipal;
                }
            }

            return returnValue;
        }

        private T RunImpersonated2<T>(MessageRpc rpc, ServiceSecurityContext securityContext, bool isSecurityContextImpersonationOn, Func<T> func)
        {
            T returnValue = default(T);
            try
            {
                if (isSecurityContextImpersonationOn)
                {
                    if (securityContext == null)
                    {
                        throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.SFxSecurityContextPropertyMissingFromRequestMessage), rpc.Request);
                    }

                    WindowsIdentity impersonationToken = securityContext.WindowsIdentity;
                    if (impersonationToken.User != null)
                    {
                        returnValue = WindowsIdentity.RunImpersonated(impersonationToken.AccessToken, func);
                    }
                    else if (securityContext.PrimaryIdentity is WindowsSidIdentity sidIdentity)
                    {
                        if (sidIdentity.SecurityIdentifier.IsWellKnown(WellKnownSidType.AnonymousSid))
                        {
                            // This requires P/Invokes to achieve on Windows. Not sure how to achieve it on Linux. For now not supporting this.
                            // A strategy is needed to cleanly move code which makes P/Invoke calls into a different package. One option might be
                            // to request support in WindowsIdentity in a future release of .NET Core and require that to use this feature.
                            throw new PlatformNotSupportedException("Anonymous impersonation");
                        }
                        else
                        {
                            string fullyQualifiedDomainName = GetUpnFromDownlevelName(sidIdentity.Name);
                            using (WindowsIdentity windowsIdentity = new WindowsIdentity(fullyQualifiedDomainName))
                            {
                                returnValue = WindowsIdentity.RunImpersonated(windowsIdentity.AccessToken, func);
                            }
                        }
                    }
                    else
                    {
                        throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityContextDoesNotAllowImpersonation, rpc.Operation.Action)), rpc.Request);
                    }
                }

                //SecurityTraceRecordHelper.TraceImpersonationSucceeded(rpc.EventTraceActivity, rpc.Operation);

                // update the impersonation succeed audit
                //if (AuditLevel.Success == (this.auditLevel & AuditLevel.Success))
                //{
                //    SecurityAuditHelper.WriteImpersonationSuccessEvent(this.auditLogLocation,
                //        this.suppressAuditFailure, rpc.Operation.Name, SecurityUtils.GetIdentityNamesFromContext(securityContext.AuthorizationContext));
                //}
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }
                //SecurityTraceRecordHelper.TraceImpersonationFailed(rpc.EventTraceActivity, rpc.Operation, ex);

                //
                // Update the impersonation failure audit
                // Copy SecurityAuthorizationBehavior.Audit level to here!!!
                //
                //                if (AuditLevel.Failure == (this.auditLevel & AuditLevel.Failure))
                //                {
                //                    try
                //                    {
                //                        string primaryIdentity;
                //                        if (securityContext != null)
                //                            primaryIdentity = SecurityUtils.GetIdentityNamesFromContext(securityContext.AuthorizationContext);
                //                        else
                //                            primaryIdentity = SecurityUtils.AnonymousIdentity.Name;

                //                        SecurityAuditHelper.WriteImpersonationFailureEvent(this.auditLogLocation,
                //                            this.suppressAuditFailure, rpc.Operation.Name, primaryIdentity, ex);
                //                    }
                //#pragma warning suppress 56500
                //                    catch (Exception auditException)
                //                    {
                //                        if (Fx.IsFatal(auditException))
                //                            throw;

                //                        DiagnosticUtility.TraceHandledException(auditException, TraceEventType.Error);
                //                    }
                //                }
                throw;
            }

            return returnValue;
        }

        private IPrincipal GetWindowsPrincipal(ServiceSecurityContext securityContext)
        {
            WindowsIdentity wid = securityContext.WindowsIdentity;
            if (!wid.IsAnonymous)
            {
                return new WindowsPrincipal(wid);
            }

            WindowsSidIdentity wsid = securityContext.PrimaryIdentity as WindowsSidIdentity;
            if (wsid != null)
            {
                return new WindowsSidPrincipal(wsid, securityContext);
            }

            return AnonymousWindowsPrincipal;
        }

        private ServiceSecurityContext GetAndCacheSecurityContext(MessageRpc rpc)
        {
            ServiceSecurityContext securityContext = rpc.SecurityContext;

            if (!rpc.HasSecurityContext)
            {
                SecurityMessageProperty securityContextProperty = rpc.Request.Properties.Security;
                if (securityContextProperty == null)
                {
                    securityContext = null; // SecurityContext.Anonymous
                }
                else
                {
                    securityContext = securityContextProperty.ServiceSecurityContext;
                    if (securityContext == null)
                    {
                        throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityContextMissing, rpc.Operation.Name)), rpc.Request);
                    }
                }

                rpc.SecurityContext = securityContext;
                rpc.HasSecurityContext = true;
            }

            return securityContext;
        }

        private string GetUpnFromDownlevelName(string downlevelName)
        {
            // On Desktop this code calls SECUR32.DLL!TranslateName to translate just the domain part of the downlevel name (DOMAIN\username) to a canonical name.
            // It then removes the trailing slash and joines username, '@' and the canonical name to create the Upn name. It then caches the DOMAIN -> canonical mapping
            // to make future lookups quicker. This is wrong and only works by happy accident of convention. Here's the breaking scenario:
            // 
            // An organization Example Inc. has multiple domains for different departments in the company. Two of them are ENGINEERING and FINANCE. The format of the usernames
            // in the domain are first name followed by initial of last name. The AD domain names for these are engineering.example.org and finance.example.org and are in the 
            // same forest. The format of the UPN account names are firstname.lastname@example.org. The engineer employee Bob Smith has a domain username of ENGINEERING\bobs but
            // his UPN name is bob.smith@example.org. The implementation on Desktop will translate his domain account name to bobs@engineering.example.org which is incorrect.
            //
            // The incorrect implementation on Desktop allows for a small cache which makes lookups really fast because of the assumptions made which will often work. When mapping
            // the domain name correctly, every account mapping would need to be cached. For now we're only caching the domain to ncname lookup and each lookup will result in a
            // call to the DC to get the mapping. There are two potential improvements that can be done here.
            //   1. Use a MRU cache to cache the full mapping.
            //   2. Further up the call stack we actually have the user SID. It might be better to lookup the user by SID instead.

            if (downlevelName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(downlevelName));
            }
            int delimiterPos = downlevelName.IndexOf('\\');
            if ((delimiterPos < 0) || (delimiterPos == 0) || (delimiterPos == downlevelName.Length - 1))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.DownlevelNameCannotMapToUpn, downlevelName)));
            }
            string shortDomainName = downlevelName.Substring(0, delimiterPos + 1);
            string userName = downlevelName.Substring(delimiterPos + 1);
            string ncName = null;
            bool found;

            // 1) Read from cache
            lock (ncNameMap)
            {
                found = ncNameMap.TryGetValue(shortDomainName, out ncName);
            }

            // 2) Not found, do expensive look up
            if (!found || s_directoryServerName == null)
            {
                using (DirectoryEntry rootDse = new DirectoryEntry("LDAP://RootDSE"))
                {
                    // No need to re-check and/or update under lock as the retrieved value will be the same each time. There's no harm
                    // if this is retrieved more than once.
                    if (s_directoryServerName == null)
                    {
                        s_directoryServerName = rootDse.Properties["dnsHostName"].Value.ToString();
                    }

                    if (!found)
                    {
                        // Retrieve the Configuration Naming Context from RootDSE
                        string configNC = rootDse.Properties["configurationNamingContext"].Value.ToString();

                        DirectoryEntry configSearchRoot = new DirectoryEntry("LDAP://" + configNC);
                        DirectorySearcher configSearch = new DirectorySearcher(configSearchRoot);
                        configSearch.Filter = $"(&(NETBIOSName={shortDomainName})(objectClass=crossRef))";

                        // Configure search to return ncname attribute
                        configSearch.PropertiesToLoad.Add("ncname");

                        SearchResult forestPartition = configSearch.FindOne();
                        if (forestPartition == null)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.DownlevelNameCannotMapToUpn, downlevelName)));
                        }

                        ncName = forestPartition.Properties["ncname"][0].ToString();

                        // Save in cache (remove a random item if cache is full)
                        lock (ncNameMap)
                        {
                            if (ncNameMap.Count >= maxDomainNameMapSize)
                            {
                                if (random == null)
                                {
                                    random = new Random(unchecked((int)DateTime.Now.Ticks));
                                }
                                int victim = random.Next() % ncNameMap.Count;
                                foreach (string key in ncNameMap.Keys)
                                {
                                    if (victim <= 0)
                                    {
                                        ncNameMap.Remove(key);
                                        break;
                                    }
                                    --victim;
                                }
                            }
                            ncNameMap[shortDomainName] = ncName;
                        }
                    }
                }
            }

            string ldapDomainEntryPath = @"LDAP://" + s_directoryServerName + @"/" + ncName;
            using (DirectoryEntry domainEntry = new DirectoryEntry(ldapDomainEntryPath))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(domainEntry))
                {
                    searcher.SearchScope = SearchScope.Subtree;
                    searcher.PropertiesToLoad.Add("userPrincipalName");
                    searcher.Filter = $"(&(objectClass=user)(samAccountName={userName}))";

                    SearchResult userResult = searcher.FindOne();
                    if (userResult == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.DownlevelNameCannotMapToUpn, downlevelName)));
                    }

                    return userResult.Properties["userPrincipalName"][0].ToString();
                }
            }
        }

        private class WindowsSidPrincipal : IPrincipal
        {
            private readonly WindowsSidIdentity identity;
            private readonly ServiceSecurityContext securityContext;

            public WindowsSidPrincipal(WindowsSidIdentity identity, ServiceSecurityContext securityContext)
            {
                this.identity = identity;
                this.securityContext = securityContext;
            }

            public IIdentity Identity
            {
                get { return identity; }
            }

            public bool IsInRole(string role)
            {
                if (role == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("role");
                }

                NTAccount account = new NTAccount(role);
                Claim claim = Claim.CreateWindowsSidClaim((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier)));
                AuthorizationContext authContext = securityContext.AuthorizationContext;
                for (int i = 0; i < authContext.ClaimSets.Count; i++)
                {
                    ClaimSet claimSet = authContext.ClaimSets[i];
                    if (claimSet.ContainsClaim(claim))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}