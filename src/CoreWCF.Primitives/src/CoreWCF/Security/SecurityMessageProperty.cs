// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SecurityMessageProperty : IMessageProperty, IDisposable
    {
        // This is the list of outgoing supporting tokens
        private Collection<SupportingTokenSpecification> outgoingSupportingTokens;
        private Collection<SupportingTokenSpecification> incomingSupportingTokens;
        private SecurityTokenSpecification transportToken;
        private SecurityTokenSpecification protectionToken;
        private SecurityTokenSpecification initiatorToken;
        private SecurityTokenSpecification recipientToken;
        private ServiceSecurityContext securityContext;
        private bool disposed = false;

        public SecurityMessageProperty()
        {
            securityContext = ServiceSecurityContext.Anonymous;
        }

        public ServiceSecurityContext ServiceSecurityContext
        {
            get
            {
                ThrowIfDisposed();
                return securityContext;
            }
            set
            {
                ThrowIfDisposed();
                securityContext = value;
            }
        }

        public ReadOnlyCollection<IAuthorizationPolicy> ExternalAuthorizationPolicies { get; set; }

        public SecurityTokenSpecification ProtectionToken
        {
            get
            {
                ThrowIfDisposed();
                return protectionToken;
            }
            set
            {
                ThrowIfDisposed();
                protectionToken = value;
            }
        }

        public SecurityTokenSpecification InitiatorToken
        {
            get
            {
                ThrowIfDisposed();
                return initiatorToken;
            }
            set
            {
                ThrowIfDisposed();
                initiatorToken = value;
            }
        }

        public SecurityTokenSpecification RecipientToken
        {
            get
            {
                ThrowIfDisposed();
                return recipientToken;
            }
            set
            {
                ThrowIfDisposed();
                recipientToken = value;
            }
        }

        public SecurityTokenSpecification TransportToken
        {
            get
            {
                ThrowIfDisposed();
                return transportToken;
            }
            set
            {
                ThrowIfDisposed();
                transportToken = value;
            }
        }


        public string SenderIdPrefix { get; set; } = "_";

        public bool HasIncomingSupportingTokens
        {
            get
            {
                ThrowIfDisposed();
                return ((incomingSupportingTokens != null) && (incomingSupportingTokens.Count > 0));
            }
        }

        public Collection<SupportingTokenSpecification> IncomingSupportingTokens
        {
            get
            {
                ThrowIfDisposed();
                if (incomingSupportingTokens == null)
                {
                    incomingSupportingTokens = new Collection<SupportingTokenSpecification>();
                }
                return incomingSupportingTokens;
            }
        }

        public Collection<SupportingTokenSpecification> OutgoingSupportingTokens
        {
            get
            {
                if (outgoingSupportingTokens == null)
                {
                    outgoingSupportingTokens = new Collection<SupportingTokenSpecification>();
                }
                return outgoingSupportingTokens;
            }
        }

        internal bool HasOutgoingSupportingTokens
        {
            get
            {
                return ((outgoingSupportingTokens != null) && (outgoingSupportingTokens.Count > 0));
            }
        }

        public IMessageProperty CreateCopy()
        {
            ThrowIfDisposed();
            SecurityMessageProperty result = new SecurityMessageProperty();

            if (HasOutgoingSupportingTokens)
            {
                for (int i = 0; i < outgoingSupportingTokens.Count; ++i)
                {
                    result.OutgoingSupportingTokens.Add(outgoingSupportingTokens[i]);
                }
            }

            if (HasIncomingSupportingTokens)
            {
                for (int i = 0; i < incomingSupportingTokens.Count; ++i)
                {
                    result.IncomingSupportingTokens.Add(incomingSupportingTokens[i]);
                }
            }

            result.securityContext = securityContext;
            result.ExternalAuthorizationPolicies = ExternalAuthorizationPolicies;
            result.SenderIdPrefix = SenderIdPrefix;

            result.protectionToken = protectionToken;
            result.initiatorToken = initiatorToken;
            result.recipientToken = recipientToken;
            result.transportToken = transportToken;

            return result;
        }

        public static SecurityMessageProperty GetOrCreate(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            SecurityMessageProperty result = null;
            if (message.Properties != null)
            {
                result = message.Properties.Security;
            }

            if (result == null)
            {
                result = new SecurityMessageProperty();
                message.Properties.Security = result;
            }

            return result;
        }

        private void AddAuthorizationPolicies(SecurityTokenSpecification spec, Collection<IAuthorizationPolicy> policies)
        {
            if (spec != null && spec.SecurityTokenPolicies != null && spec.SecurityTokenPolicies.Count > 0)
            {
                for (int i = 0; i < spec.SecurityTokenPolicies.Count; ++i)
                {
                    policies.Add(spec.SecurityTokenPolicies[i]);
                }
            }
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> GetInitiatorTokenAuthorizationPolicies()
        {
            return GetInitiatorTokenAuthorizationPolicies(true);
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> GetInitiatorTokenAuthorizationPolicies(bool includeTransportToken)
        {
            return GetInitiatorTokenAuthorizationPolicies(includeTransportToken, null);
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> GetInitiatorTokenAuthorizationPolicies(bool includeTransportToken, SecurityContextSecurityToken supportingSessionTokenToExclude)
        {
            // fast path
            if (!HasIncomingSupportingTokens)
            {
                if (transportToken != null && initiatorToken == null && protectionToken == null)
                {
                    if (includeTransportToken && transportToken.SecurityTokenPolicies != null)
                    {
                        return transportToken.SecurityTokenPolicies;
                    }
                    else
                    {
                        return EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
                    }
                }
                else if (transportToken == null && initiatorToken != null && protectionToken == null)
                {
                    return initiatorToken.SecurityTokenPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
                }
                else if (transportToken == null && initiatorToken == null && protectionToken != null)
                {
                    return protectionToken.SecurityTokenPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
                }
            }

            Collection<IAuthorizationPolicy> policies = new Collection<IAuthorizationPolicy>();
            if (includeTransportToken)
            {
                AddAuthorizationPolicies(transportToken, policies);
            }
            AddAuthorizationPolicies(initiatorToken, policies);
            AddAuthorizationPolicies(protectionToken, policies);
            if (HasIncomingSupportingTokens)
            {
                for (int i = 0; i < incomingSupportingTokens.Count; ++i)
                {
                    if (supportingSessionTokenToExclude != null)
                    {
                        SecurityContextSecurityToken sct = incomingSupportingTokens[i].SecurityToken as SecurityContextSecurityToken;
                        if (sct != null && sct.ContextId == supportingSessionTokenToExclude.ContextId)
                        {
                            continue;
                        }
                    }
                    SecurityTokenAttachmentMode attachmentMode = incomingSupportingTokens[i].SecurityTokenAttachmentMode;
                    // a safety net in case more attachment modes get added to the product without 
                    // reviewing this code.
                    if (attachmentMode == SecurityTokenAttachmentMode.Endorsing
                        || attachmentMode == SecurityTokenAttachmentMode.Signed
                        || attachmentMode == SecurityTokenAttachmentMode.SignedEncrypted
                        || attachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                    {
                        AddAuthorizationPolicies(incomingSupportingTokens[i], policies);
                    }
                }
            }
            return new ReadOnlyCollection<IAuthorizationPolicy>(policies);
        }

        public void Dispose()
        {
            // do no-op for future V2
            if (!disposed)
            {
                disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }

}
