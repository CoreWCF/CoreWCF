using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Channels;
using CoreWCF.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Text;

namespace CoreWCF
{
    public class ServiceSecurityContext
    {
        static ServiceSecurityContext anonymous;
        ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
        AuthorizationContext authorizationContext;
        IIdentity primaryIdentity;
        Claim identityClaim;
        WindowsIdentity windowsIdentity;

        // Perf: delay created authorizationContext using forward chain.
        internal ServiceSecurityContext(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("authorizationPolicies");
            }
            authorizationContext = null;
            this.authorizationPolicies = authorizationPolicies;
        }

        internal ServiceSecurityContext(AuthorizationContext authorizationContext)
            : this(authorizationContext, EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance)
        {
        }

        internal ServiceSecurityContext(AuthorizationContext authorizationContext, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("authorizationContext");
            }
            if (authorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("authorizationPolicies");
            }
            this.authorizationContext = authorizationContext;
            this.authorizationPolicies = authorizationPolicies;
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
                if (this.primaryIdentity == null)
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
                return this.primaryIdentity;
            }
        }

        public WindowsIdentity WindowsIdentity
        {
            get
            {
                if (this.windowsIdentity == null)
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
                return this.windowsIdentity;
            }
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> AuthorizationPolicies
        {
            get
            {
                return authorizationPolicies;
            }
            set
            {
                authorizationPolicies = value;
            }
        }

        internal AuthorizationContext AuthorizationContext
        {
            get
            {
                if (authorizationContext == null)
                {
                    authorizationContext = AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies);
                }
                return authorizationContext;
            }
        }

        IList<IIdentity> GetIdentities()
        {
            object identities;
            AuthorizationContext authContext = AuthorizationContext;
            if (authContext != null && authContext.Properties.TryGetValue(Security.SecurityUtils.Identities, out identities))
            {
                return identities as IList<IIdentity>;
            }
            return null;
        }
    }

}
