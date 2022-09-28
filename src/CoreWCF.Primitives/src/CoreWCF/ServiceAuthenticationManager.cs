// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF
{
    public class ServiceAuthenticationManager
    {
        [Obsolete("Implementers should override AuthenticateAsync.")]
        public virtual ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            return authPolicy;
        }

        public virtual ValueTask<(ReadOnlyCollection<IAuthorizationPolicy> policies, Message message)> AuthenticateAsync(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, Message message)
        {
            var policies = Authenticate(authPolicy, listenUri, ref message);
            return new ValueTask<(ReadOnlyCollection<IAuthorizationPolicy> policies, Message message)>((policies, message));
        }
    }

    internal class SCTServiceAuthenticationManagerWrapper : ServiceAuthenticationManager
    {
        private readonly ServiceAuthenticationManager _wrappedAuthenticationManager;

        internal SCTServiceAuthenticationManagerWrapper(ServiceAuthenticationManager wrappedServiceAuthManager)
        {
            _wrappedAuthenticationManager = wrappedServiceAuthManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappedServiceAuthManager));
        }

        public override async ValueTask<(ReadOnlyCollection<IAuthorizationPolicy> policies, Message message)> AuthenticateAsync(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, Message message)
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

            return await _wrappedAuthenticationManager.AuthenticateAsync(authPolicy, listenUri, message);
        }
    }

    internal class ServiceAuthenticationManagerWrapper : ServiceAuthenticationManager
    {
        private readonly ServiceAuthenticationManager _wrappedAuthenticationManager;
        private readonly string[] _filteredActionUriCollection;

        internal ServiceAuthenticationManagerWrapper(ServiceAuthenticationManager wrappedServiceAuthManager, string[] actionUriFilter)
        {
            if ((actionUriFilter != null) && (actionUriFilter.Length > 0))
            {
                _filteredActionUriCollection = new string[actionUriFilter.Length];
                for (int i = 0; i < actionUriFilter.Length; ++i)
                {
                    _filteredActionUriCollection[i] = actionUriFilter[i];
                }
            }

            _wrappedAuthenticationManager = wrappedServiceAuthManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappedServiceAuthManager));
        }

        public override async ValueTask<(ReadOnlyCollection<IAuthorizationPolicy> policies, Message message)> AuthenticateAsync(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, Message message)
        {
            if (CanSkipAuthentication(message))
            {
                return (authPolicy, message);
            }

            if (_filteredActionUriCollection != null)
            {
                for (int i = 0; i < _filteredActionUriCollection.Length; ++i)
                {
                    if ((message != null) &&
                        (message.Headers != null) &&
                        !string.IsNullOrEmpty(message.Headers.Action) &&
                        (message.Headers.Action == _filteredActionUriCollection[i]))
                    {
                        return (authPolicy, message);
                    }
                }
            }

            return await _wrappedAuthenticationManager.AuthenticateAsync(authPolicy, listenUri, message);
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
