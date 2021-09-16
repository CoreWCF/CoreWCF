// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    /// <summary>
    /// Wraps a SessionSecurityTokenHandler. Delegates the token authentication call to
    /// this wrapped tokenAuthenticator. Wraps the returned ClaimsIdentities into
    /// an IAuthorizationPolicy. This class is wired into WCF and actually receives 
    /// SecurityContextSecurityTokens which are then wrapped into SessionSecurityTokens for
    /// validation.
    /// </summary>
    internal class WrappedSessionSecurityTokenAuthenticator : SecurityTokenAuthenticator, IIssuanceSecurityTokenAuthenticator, ICommunicationObject
    {
        private readonly SessionSecurityTokenHandler _sessionTokenHandler;
        private readonly IIssuanceSecurityTokenAuthenticator _issuanceSecurityTokenAuthenticator;
        private readonly ICommunicationObject _communicationObject;
        private readonly SctClaimsHandler _sctClaimsHandler;
        private readonly ExceptionMapper _exceptionMapper;

        public event EventHandler Closed;
        public event EventHandler Faulted;

        /// <summary>
        /// Initializes an instance of <see cref="WrappedRsaSecurityTokenAuthenticator"/>
        /// </summary>
        /// <param name="sessionTokenHandler">The sessionTokenHandler to wrap</param>
        /// <param name="wcfSessionAuthenticator">The wcf SessionTokenAuthenticator.</param>
        /// <param name="sctClaimsHandler">Handler that converts WCF generated IAuthorizationPolicy to <see cref="AuthorizationPolicy"/></param>
        /// <param name="exceptionMapper">Converts token validation exception to SOAP faults.</param>
        public WrappedSessionSecurityTokenAuthenticator(SessionSecurityTokenHandler sessionTokenHandler,
                                                         SecurityTokenAuthenticator wcfSessionAuthenticator,
                                                         SctClaimsHandler sctClaimsHandler,
                                                         ExceptionMapper exceptionMapper)
            : base()
        {
            if (wcfSessionAuthenticator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wcfSessionAuthenticator));
            }

            _issuanceSecurityTokenAuthenticator = wcfSessionAuthenticator as IIssuanceSecurityTokenAuthenticator;
            if (_issuanceSecurityTokenAuthenticator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4244));
            }

            _communicationObject = wcfSessionAuthenticator as ICommunicationObject;
            if (_communicationObject == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4245));
            }

            _sessionTokenHandler = sessionTokenHandler ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(sessionTokenHandler));
            _sctClaimsHandler = sctClaimsHandler ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(sctClaimsHandler));

            _exceptionMapper = exceptionMapper ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exceptionMapper));
        }

        /// <summary>
        /// Validates the token using the wrapped token handler and generates IAuthorizationPolicy
        /// wrapping the returned ClaimsIdentities.
        /// </summary>
        /// <param name="token">Token to be validated. This is always a SecurityContextSecurityToken.</param>
        /// <returns>Read-only collection of IAuthorizationPolicy</returns>
        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            SecurityContextSecurityToken sct = token as SecurityContextSecurityToken;
            SessionSecurityToken sessionToken = SecurityContextSecurityTokenHelper.ConvertSctToSessionToken(sct);
            IEnumerable<ClaimsIdentity> identities = null;

            try
            {
                identities = _sessionTokenHandler.ValidateToken(sessionToken, _sctClaimsHandler.EndpointId);
            }
            catch (Exception ex)
            {
                if (!_exceptionMapper.HandleSecurityTokenProcessingException(ex))
                {
                    throw;
                }
            }

            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(new List<IAuthorizationPolicy>(new AuthorizationPolicy[] { new AuthorizationPolicy(identities) }).AsReadOnly());
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (token is SecurityContextSecurityToken);
        }

        /// <summary>
        /// The SecurityServiceDispatcher is updated to communication obj which is SecuritySessionSecurityTokenAuthenticator
        /// </summary>
        /// <param name="securitySeviceDispatcher"></param>
        public void SetSecureServiceDispatcher(SecurityServiceDispatcher securitySeviceDispatcher)
        {
            if (_communicationObject == null || !(_communicationObject is SecuritySessionSecurityTokenAuthenticator))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4245));
            }
            var authenticator = (SecuritySessionSecurityTokenAuthenticator)_communicationObject;
            authenticator.SecurityServiceDispatcher = securitySeviceDispatcher;
        }

        #region IIssuanceSecurityTokenAuthenticator Members

        public IssuedSecurityTokenHandler IssuedSecurityTokenHandler
        {
            get
            {
                return _issuanceSecurityTokenAuthenticator.IssuedSecurityTokenHandler;
            }
            set
            {
                _issuanceSecurityTokenAuthenticator.IssuedSecurityTokenHandler = value;
            }
        }

        public RenewedSecurityTokenHandler RenewedSecurityTokenHandler
        {
            get
            {
                return _issuanceSecurityTokenAuthenticator.RenewedSecurityTokenHandler;
            }
            set
            {
                _issuanceSecurityTokenAuthenticator.RenewedSecurityTokenHandler = value;
            }
        }

        #endregion


        #region ICommunicationObject Members

        // all these methods are passthroughs

        public void Abort()
        {
            _communicationObject.Abort();
        }

        public event System.EventHandler Closing
        {
            add { _communicationObject.Closing += value; }
            remove { _communicationObject.Closing -= value; }
        }

        public Task CloseAsync()
        {
           return _communicationObject.CloseAsync();
        }
        public Task CloseAsync(CancellationToken token)
        {
            return _communicationObject.CloseAsync(token);
        }
        public Task OpenAsync()
        {
            return _communicationObject.OpenAsync();
        }
        public Task OpenAsync(CancellationToken token)
        {
            return _communicationObject.OpenAsync(token);
        }

        public event System.EventHandler Opened
        {
            add { _communicationObject.Opened += value; }
            remove { _communicationObject.Opened -= value; }
        }

        public event System.EventHandler Opening
        {
            add { _communicationObject.Opening += value; }
            remove { _communicationObject.Opening -= value; }
        }

        public CommunicationState State
        {

            get { return _communicationObject.State; }
        }

        #endregion
    }

    /// <summary>
    /// Defines a SecurityStateEncoder whose Encode and Decode operations are 
    /// a no-op. This class is used to null WCF SecurityContextToken creation
    /// code to skip any encryption and decryption cost. When SessionSecurityTokenHandler
    /// is being used we will use our own EncryptionTransform and ignore the WCF 
    /// generated cookie.
    /// </summary>
    internal class NoOpSecurityStateEncoder : SecurityStateEncoder
    {
        protected internal override byte[] EncodeSecurityState(byte[] data)
        {
            return data;
        }

        protected internal override byte[] DecodeSecurityState(byte[] data)
        {
            return data;
        }
    }
}
