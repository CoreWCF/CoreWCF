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
        private Collection<SupportingTokenSpecification> _outgoingSupportingTokens;
        private Collection<SupportingTokenSpecification> _incomingSupportingTokens;
        private SecurityTokenSpecification _transportToken;
        private SecurityTokenSpecification _protectionToken;
        private SecurityTokenSpecification _initiatorToken;
        private SecurityTokenSpecification _recipientToken;
        private ServiceSecurityContext _securityContext;
        private bool _disposed = false;

        public SecurityMessageProperty()
        {
            _securityContext = ServiceSecurityContext.Anonymous;
        }

        public ServiceSecurityContext ServiceSecurityContext
        {
            get
            {
                ThrowIfDisposed();
                return _securityContext;
            }
            set
            {
                ThrowIfDisposed();
                _securityContext = value;
            }
        }

        public ReadOnlyCollection<IAuthorizationPolicy> ExternalAuthorizationPolicies { get; set; }

        public SecurityTokenSpecification ProtectionToken
        {
            get
            {
                ThrowIfDisposed();
                return _protectionToken;
            }
            set
            {
                ThrowIfDisposed();
                _protectionToken = value;
            }
        }

        public SecurityTokenSpecification InitiatorToken
        {
            get
            {
                ThrowIfDisposed();
                return _initiatorToken;
            }
            set
            {
                ThrowIfDisposed();
                _initiatorToken = value;
            }
        }

        public SecurityTokenSpecification RecipientToken
        {
            get
            {
                ThrowIfDisposed();
                return _recipientToken;
            }
            set
            {
                ThrowIfDisposed();
                _recipientToken = value;
            }
        }

        public SecurityTokenSpecification TransportToken
        {
            get
            {
                ThrowIfDisposed();
                return _transportToken;
            }
            set
            {
                ThrowIfDisposed();
                _transportToken = value;
            }
        }


        public string SenderIdPrefix { get; set; } = "_";

        public bool HasIncomingSupportingTokens
        {
            get
            {
                ThrowIfDisposed();
                return ((_incomingSupportingTokens != null) && (_incomingSupportingTokens.Count > 0));
            }
        }

        public Collection<SupportingTokenSpecification> IncomingSupportingTokens
        {
            get
            {
                ThrowIfDisposed();
                if (_incomingSupportingTokens == null)
                {
                    _incomingSupportingTokens = new Collection<SupportingTokenSpecification>();
                }
                return _incomingSupportingTokens;
            }
        }

        public Collection<SupportingTokenSpecification> OutgoingSupportingTokens
        {
            get
            {
                if (_outgoingSupportingTokens == null)
                {
                    _outgoingSupportingTokens = new Collection<SupportingTokenSpecification>();
                }
                return _outgoingSupportingTokens;
            }
        }

        internal bool HasOutgoingSupportingTokens
        {
            get
            {
                return ((_outgoingSupportingTokens != null) && (_outgoingSupportingTokens.Count > 0));
            }
        }

        public IMessageProperty CreateCopy()
        {
            ThrowIfDisposed();
            SecurityMessageProperty result = new SecurityMessageProperty();

            if (HasOutgoingSupportingTokens)
            {
                for (int i = 0; i < _outgoingSupportingTokens.Count; ++i)
                {
                    result.OutgoingSupportingTokens.Add(_outgoingSupportingTokens[i]);
                }
            }

            if (HasIncomingSupportingTokens)
            {
                for (int i = 0; i < _incomingSupportingTokens.Count; ++i)
                {
                    result.IncomingSupportingTokens.Add(_incomingSupportingTokens[i]);
                }
            }

            result._securityContext = _securityContext;
            result.ExternalAuthorizationPolicies = ExternalAuthorizationPolicies;
            result.SenderIdPrefix = SenderIdPrefix;

            result._protectionToken = _protectionToken;
            result._initiatorToken = _initiatorToken;
            result._recipientToken = _recipientToken;
            result._transportToken = _transportToken;

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
                if (_transportToken != null && _initiatorToken == null && _protectionToken == null)
                {
                    if (includeTransportToken && _transportToken.SecurityTokenPolicies != null)
                    {
                        return _transportToken.SecurityTokenPolicies;
                    }
                    else
                    {
                        return EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
                    }
                }
                else if (_transportToken == null && _initiatorToken != null && _protectionToken == null)
                {
                    return _initiatorToken.SecurityTokenPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
                }
                else if (_transportToken == null && _initiatorToken == null && _protectionToken != null)
                {
                    return _protectionToken.SecurityTokenPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
                }
            }

            Collection<IAuthorizationPolicy> policies = new Collection<IAuthorizationPolicy>();
            if (includeTransportToken)
            {
                AddAuthorizationPolicies(_transportToken, policies);
            }
            AddAuthorizationPolicies(_initiatorToken, policies);
            AddAuthorizationPolicies(_protectionToken, policies);
            if (HasIncomingSupportingTokens)
            {
                for (int i = 0; i < _incomingSupportingTokens.Count; ++i)
                {
                    if (supportingSessionTokenToExclude != null)
                    {
                        SecurityContextSecurityToken sct = _incomingSupportingTokens[i].SecurityToken as SecurityContextSecurityToken;
                        if (sct != null && sct.ContextId == supportingSessionTokenToExclude.ContextId)
                        {
                            continue;
                        }
                    }
                    SecurityTokenAttachmentMode attachmentMode = _incomingSupportingTokens[i].SecurityTokenAttachmentMode;
                    // a safety net in case more attachment modes get added to the product without 
                    // reviewing this code.
                    if (attachmentMode == SecurityTokenAttachmentMode.Endorsing
                        || attachmentMode == SecurityTokenAttachmentMode.Signed
                        || attachmentMode == SecurityTokenAttachmentMode.SignedEncrypted
                        || attachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                    {
                        AddAuthorizationPolicies(_incomingSupportingTokens[i], policies);
                    }
                }
            }
            return new ReadOnlyCollection<IAuthorizationPolicy>(policies);
        }

        public void Dispose()
        {
            // do no-op for future V2
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }
}
