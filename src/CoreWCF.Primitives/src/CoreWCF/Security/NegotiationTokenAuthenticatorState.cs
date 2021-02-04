// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class NegotiationTokenAuthenticatorState : IDisposable
    {
        private bool _isNegotiationCompleted;
        private SecurityContextSecurityToken _serviceToken;
        private readonly object _thisLock;

        public NegotiationTokenAuthenticatorState() => _thisLock = new object();

        public object ThisLock => _thisLock;

        public bool IsNegotiationCompleted => _isNegotiationCompleted;

        public SecurityContextSecurityToken ServiceToken
        {
            get
            {
                CheckCompleted();
                return _serviceToken;
            }
        }

        public virtual void Dispose() { }

        public void SetServiceToken(SecurityContextSecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            _serviceToken = token;
            _isNegotiationCompleted = true;
        }

        public virtual string GetRemoteIdentityName()
        {
            if (_isNegotiationCompleted)
            {
                return SecurityUtils.GetIdentityNamesFromPolicies(_serviceToken.AuthorizationPolicies);
            }
            return string.Empty;
        }

        private void CheckCompleted()
        {
            if (!_isNegotiationCompleted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.NegotiationIsNotCompleted)));
            }
        }
    }
}
