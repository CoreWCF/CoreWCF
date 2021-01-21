// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF
{
    public class ServiceAuthenticationManager
    {
        public virtual ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            return authPolicy;
        }
    }

    internal class SCTServiceAuthenticationManagerWrapper : ServiceAuthenticationManager
    {
        private readonly ServiceAuthenticationManager wrappedAuthenticationManager;

        internal SCTServiceAuthenticationManagerWrapper(ServiceAuthenticationManager wrappedServiceAuthManager)
        {
            if (wrappedServiceAuthManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("wrappedServiceAuthManager");
            }

            wrappedAuthenticationManager = wrappedServiceAuthManager;
        }

        public override ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            if ((message != null) &&
                (message.Properties != null) &&
                (message.Properties.Security != null) &&
                (message.Properties.Security.TransportToken != null) &&
                (message.Properties.Security.ServiceSecurityContext != null) &&
                (message.Properties.Security.ServiceSecurityContext.AuthorizationPolicies != null))
            {
                List<IAuthorizationPolicy> authPolicies = new List<IAuthorizationPolicy>(message.Properties.Security.ServiceSecurityContext.AuthorizationPolicies);
                foreach (IAuthorizationPolicy policy in message.Properties.Security.TransportToken.SecurityTokenPolicies)
                {
                    authPolicies.Remove(policy);
                }
                authPolicy = authPolicies.AsReadOnly();
            }

            return wrappedAuthenticationManager.Authenticate(authPolicy, listenUri, ref message);
        }
    }

    internal class ServiceAuthenticationManagerWrapper : ServiceAuthenticationManager
    {
        private readonly ServiceAuthenticationManager wrappedAuthenticationManager;
        private readonly string[] filteredActionUriCollection;

        internal ServiceAuthenticationManagerWrapper(ServiceAuthenticationManager wrappedServiceAuthManager, string[] actionUriFilter)
        {
            if (wrappedServiceAuthManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("wrappedServiceAuthManager");
            }

            if ((actionUriFilter != null) && (actionUriFilter.Length > 0))
            {
                filteredActionUriCollection = new string[actionUriFilter.Length];
                for (int i = 0; i < actionUriFilter.Length; ++i)
                {
                    filteredActionUriCollection[i] = actionUriFilter[i];
                }
            }

            wrappedAuthenticationManager = wrappedServiceAuthManager;
        }

        public override ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            if (CanSkipAuthentication(message))
            {
                return authPolicy;
            }

            if (filteredActionUriCollection != null)
            {
                for (int i = 0; i < filteredActionUriCollection.Length; ++i)
                {
                    if ((message != null) &&
                        (message.Headers != null) &&
                        !String.IsNullOrEmpty(message.Headers.Action) &&
                        (message.Headers.Action == filteredActionUriCollection[i]))
                    {
                        return authPolicy;
                    }
                }
            }

            return wrappedAuthenticationManager.Authenticate(authPolicy, listenUri, ref message);
        }

        //
        // We skip the authentication step if the client already has an SCT and there are no Transport level tokens.
        // ServiceAuthenticationManager would have been called when the SCT was issued and there is no need to do
        // Authentication again. If TransportToken was present then we would call ServiceAutenticationManager as 
        // TransportTokens are not authenticated during SCT issuance.
        //
        private bool CanSkipAuthentication(Message message)
        {
            if ((message != null) && (message.Properties != null) && (message.Properties.Security != null) && (message.Properties.Security.TransportToken == null))
            {
                if ((message.Properties.Security.ProtectionToken != null) &&
                    (message.Properties.Security.ProtectionToken.SecurityToken != null) &&
                    (message.Properties.Security.ProtectionToken.SecurityToken.GetType() == typeof(SecurityContextSecurityToken)))
                {
                    return true;
                }

                if (message.Properties.Security.HasIncomingSupportingTokens)
                {
                    foreach (SupportingTokenSpecification tokenSpecification in message.Properties.Security.IncomingSupportingTokens)
                    {
                        if ((tokenSpecification.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing) &&
                            (tokenSpecification.SecurityToken.GetType() == typeof(SecurityContextSecurityToken)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
