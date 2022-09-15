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
        private static ServiceSecurityContext s_anonymous;
        private AuthorizationContext _authorizationContext;
        private IIdentity _primaryIdentity;
        private Claim _identityClaim;
        private WindowsIdentity _windowsIdentity;

        // Perf: delay created authorizationContext using forward chain.
        public ServiceSecurityContext(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            _authorizationContext = null;
            AuthorizationPolicies = authorizationPolicies ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationPolicies));
        }

        public ServiceSecurityContext(AuthorizationContext authorizationContext)
            : this(authorizationContext, EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance)
        {
        }

        public ServiceSecurityContext(AuthorizationContext authorizationContext, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            _authorizationContext = authorizationContext ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationContext));
            AuthorizationPolicies = authorizationPolicies ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationPolicies));
        }

        public static ServiceSecurityContext Anonymous
        {
            get
            {
                if (s_anonymous == null)
                {
                    s_anonymous = new ServiceSecurityContext(EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance);
                }
                return s_anonymous;
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
                if (_identityClaim == null)
                {
                    _identityClaim = SecurityUtils.GetPrimaryIdentityClaim(AuthorizationContext);
                }
                return _identityClaim;
            }
        }

        public IIdentity PrimaryIdentity
        {
            get
            {
                if (_primaryIdentity == null)
                {
                    IIdentity primaryIdentity = null;
                    IList<IIdentity> identities = GetIdentities();
                    // Multiple Identities is treated as anonymous
                    if (identities != null && identities.Count == 1)
                    {
                        primaryIdentity = identities[0];
                    }

                    _primaryIdentity = primaryIdentity ?? SecurityUtils.AnonymousIdentity;
                }
                return _primaryIdentity;
            }
        }

        public WindowsIdentity WindowsIdentity
        {
            get
            {
                if (_windowsIdentity == null)
                {
                    WindowsIdentity windowsIdentity = null;
                    IList<IIdentity> identities = GetIdentities();
                    if (identities != null)
                    {
                        for (int i = 0; i < identities.Count; ++i)
                        {
                            if (identities[i] is WindowsIdentity identity)
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

                    _windowsIdentity = windowsIdentity ?? WindowsIdentity.GetAnonymous();
                }
                return _windowsIdentity;
            }
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> AuthorizationPolicies { get; set; }

        public AuthorizationContext AuthorizationContext
        {
            get
            {
                if (_authorizationContext == null)
                {
                    _authorizationContext = AuthorizationContext.CreateDefaultAuthorizationContext(AuthorizationPolicies);
                }
                return _authorizationContext;
            }
        }

        public IList<IIdentity> GetIdentities()
        {
            AuthorizationContext authContext = AuthorizationContext;
            if (authContext != null && authContext.Properties.TryGetValue(SecurityUtils.Identities, out object identities))
            {
                return identities as IList<IIdentity>;
            }
            return null;
        }
    }
}
