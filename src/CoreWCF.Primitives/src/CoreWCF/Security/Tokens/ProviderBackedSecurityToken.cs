// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    /// <summary>
    /// The ProviderBackedSecurityToken was added for the ChannelBindingToken work for Win7.  
    /// It is used to delay the resolution of a token until it is needed.  
    /// For the CBT, this delay is necessary as the CBT is not available until SecurityAppliedMessage.OnWriteMessage is called.
    /// The CBT binds a token to the 
    /// </summary>
    internal class ProviderBackedSecurityToken : SecurityToken
    {
        // Double-checked locking pattern requires volatile for read/write synchronization
        private readonly SecurityToken _securityToken;
        private TimeSpan _timeout;
        private ChannelBinding _channelBinding;
        private readonly object _lock;

        /// <summary>
        /// Constructor to create an instance of this class.
        /// </summary>
        /// <param name="securityToken">SecurityToken that represents the SecurityTokenElement element.</param>
        public ProviderBackedSecurityToken(SecurityTokenProvider tokenProvider, TimeSpan timeout)
        {
            _lock = new object();

            if (tokenProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(tokenProvider)));
            }

            TokenProvider = tokenProvider;
            _timeout = timeout;
        }

        public SecurityTokenProvider TokenProvider { get; }

        public ChannelBinding ChannelBinding
        {
            set { _channelBinding = value; }
        }

        private void ResolveSecurityToken()
        {
            throw new NotImplementedException();
            //TODO
            //if ( _securityToken == null )
            //{
            //    lock ( _lock )
            //    {
            //        if ( _securityToken == null )
            //        {

            //            ClientCredentialsSecurityTokenManager.KerberosSecurityTokenProviderWrapper kerbTokenProvider = _tokenProvider 
            //                                            as ClientCredentialsSecurityTokenManager.KerberosSecurityTokenProviderWrapper;
            //            if (kerbTokenProvider != null)
            //            {
            //                _securityToken = kerbTokenProvider.GetToken((new TimeoutHelper(_timeout)).RemainingTime(), _channelBinding);
            //            }
            //            else
            //            {
            //                _securityToken = _tokenProvider.GetToken((new TimeoutHelper(_timeout)).RemainingTime());
            //            }
            //        }
            //    }
            //}

            //if ( _securityToken == null )
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError( new SecurityTokenException( SR.Format( SR.SecurityTokenNotResolved, _tokenProvider.GetType().ToString() ) ) );
            //}

            //return;
        }

        public SecurityToken Token
        {
            get
            {
                if (_securityToken == null)
                {
                    ResolveSecurityToken();
                }

                return _securityToken;
            }
        }

        public override string Id
        {
            get
            {
                if (_securityToken == null)
                {
                    ResolveSecurityToken();
                }

                return _securityToken.Id;
            }
        }

        public override System.Collections.ObjectModel.ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get
            {
                if (_securityToken == null)
                {
                    ResolveSecurityToken();
                }

                return _securityToken.SecurityKeys;
            }
        }

        public override DateTime ValidFrom
        {
            get
            {
                if (_securityToken == null)
                {
                    ResolveSecurityToken();
                }

                return _securityToken.ValidFrom;
            }
        }

        public override DateTime ValidTo
        {
            get
            {
                if (_securityToken == null)
                {
                    ResolveSecurityToken();
                }

                return _securityToken.ValidTo;
            }
        }
    }
}
