// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;

namespace CoreWCF
{
    public class ServiceSecurityContext
    {
        private static ServiceSecurityContext anonymous;
        private AuthorizationContext authorizationContext;
        private IIdentity primaryIdentity;
        private Claim identityClaim;
        private WindowsIdentity windowsIdentity;

        // Perf: delay created authorizationContext using forward chain.
        public ServiceSecurityContext(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationPolicies));
            }
            authorizationContext = null;
            AuthorizationPolicies = authorizationPolicies;
        }

        public ServiceSecurityContext(AuthorizationContext authorizationContext)
            : this(authorizationContext, EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance)
        {
        }

        public ServiceSecurityContext(AuthorizationContext authorizationContext, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationContext));
            }
            if (authorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationPolicies));
            }
            this.authorizationContext = authorizationContext;
            AuthorizationPolicies = authorizationPolicies;
        }

        public static ServiceSecurityContext Anonymous
        {
            get
            {
                if (anonymous == null)
                {
                    anonymous = new ServiceSecurityContext(EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance);
                }
                return anonymous;
            }
        }

        public static ServiceSecurityContext Current
        {
            get
            {
                ServiceSecurityContext result = null;

                OperationContext operationContext = OperationContext.Current;
                if (operationContext != null)
                {
                    MessageProperties properties = operationContext.IncomingMessageProperties;
                    if (properties != null)
                    {
                        SecurityMessageProperty security = properties.Security;
                        if (security != null)
                        {
                            result = security.ServiceSecurityContext;
                        }
                    }
                }

                return result;
            }
        }

        public bool IsAnonymous
        {
            get
            {
                return this == Anonymous || IdentityClaim == null;
            }
        }

        // TODO: Claim is from IdentityModel but I needed to expose it. This needs to be resolved.
        public Claim IdentityClaim
        {
            get
            {
                if (identityClaim == null)
                {
                    identityClaim = Security.SecurityUtils.GetPrimaryIdentityClaim(AuthorizationContext);
                }
                return identityClaim;
            }
        }

        public IIdentity PrimaryIdentity
        {
            get
            {
                if (primaryIdentity == null)
                {
                    IIdentity primaryIdentity = null;
                    IList<IIdentity> identities = GetIdentities();
                    // Multiple Identities is treated as anonymous
                    if (identities != null && identities.Count == 1)
                    {
                        primaryIdentity = identities[0];
                    }

                    this.primaryIdentity = primaryIdentity ?? Security.SecurityUtils.AnonymousIdentity;
                }
                return primaryIdentity;
            }
        }

        public WindowsIdentity WindowsIdentity
        {
            get
            {
                if (windowsIdentity == null)
                {
                    WindowsIdentity windowsIdentity = null;
                    IList<IIdentity> identities = GetIdentities();
                    if (identities != null)
                    {
                        for (int i = 0; i < identities.Count; ++i)
                        {
                            WindowsIdentity identity = identities[i] as WindowsIdentity;
                            if (identity != null)
                            {
                                // Multiple Identities is treated as anonymous
                                if (windowsIdentity != null)
                                {
                                    windowsIdentity = WindowsIdentity.GetAnonymous();
                                    break;
                                }
                                windowsIdentity = identity;
                            }
                        }
                    }

                    this.windowsIdentity = windowsIdentity ?? WindowsIdentity.GetAnonymous();
                }
                return windowsIdentity;
            }
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> AuthorizationPolicies { get; set; }

        public AuthorizationContext AuthorizationContext
        {
            get
            {
                if (authorizationContext == null)
                {
                    authorizationContext = AuthorizationContext.CreateDefaultAuthorizationContext(AuthorizationPolicies);
                }
                return authorizationContext;
            }
        }

        private IList<IIdentity> GetIdentities()
        {
            AuthorizationContext authContext = AuthorizationContext;
            if (authContext != null && authContext.Properties.TryGetValue(Security.SecurityUtils.Identities, out object identities))
            {
                return identities as IList<IIdentity>;
            }
            return null;
        }
    }

}
