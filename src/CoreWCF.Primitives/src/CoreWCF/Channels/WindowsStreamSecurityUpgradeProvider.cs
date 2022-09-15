// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Description;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    internal class WindowsStreamSecurityUpgradeProvider : StreamSecurityUpgradeProvider
    {
        private EndpointIdentity _identity;
        private readonly SecurityTokenManager _securityTokenManager;
        private readonly bool _isClient;
        private readonly Uri _listenUri;

        public WindowsStreamSecurityUpgradeProvider(WindowsStreamSecurityBindingElement bindingElement,
            BindingContext context, bool isClient)
            : base(context.Binding)
        {
            ExtractGroupsForWindowsAccounts = TransportDefaults.ExtractGroupsForWindowsAccounts;
            ProtectionLevel = bindingElement.ProtectionLevel;
            Scheme = context.Binding.Scheme;
            _isClient = isClient;
            _listenUri = TransportSecurityHelpers.GetListenUri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);

            SecurityCredentialsManager credentialProvider = context.BindingParameters.Find<SecurityCredentialsManager>();
            if (credentialProvider == null)
            {
                //if (isClient)
                //{
                //    credentialProvider = ClientCredentials.CreateDefaultCredentials();
                //}
                //else
                //{
                credentialProvider = new ServiceCredentials(); //ServiceCredentials.CreateDefaultCredentials();
                //}
            }

           if(credentialProvider is ServiceCredentials)
            {
                ServiceCredentials serviceCred = (ServiceCredentials)credentialProvider;
                LdapSettings = serviceCred.WindowsAuthentication.LdapSetting;
            }
           _securityTokenManager = credentialProvider.CreateSecurityTokenManager();
        }

        public string Scheme { get; }

        internal bool ExtractGroupsForWindowsAccounts { get; private set; }

        public override EndpointIdentity Identity
        {
            get
            {
                // If the server credential is null, then we have not been opened yet and have no identity to expose.
                if (ServerCredential != null)
                {
                    if (_identity == null)
                    {
                        lock (ThisLock)
                        {
                            if (_identity == null)
                            {
                                _identity = SecurityUtils.CreateWindowsIdentity(ServerCredential);
                            }
                        }
                    }
                }
                return _identity;
            }
        }

        internal IdentityVerifier IdentityVerifier { get; private set; }

        public ProtectionLevel ProtectionLevel { get; }

        private NetworkCredential ServerCredential { get; set; }

        protected LdapSettings LdapSettings { get; private set; }

        public override StreamUpgradeAcceptor CreateUpgradeAcceptor()
        {
            ThrowIfDisposedOrNotOpen();
            return new WindowsStreamSecurityUpgradeAcceptor(this);
        }

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            if (!_isClient)
            {
                SecurityTokenRequirement sspiTokenRequirement = TransportSecurityHelpers.CreateSspiTokenRequirement(Scheme, _listenUri);
                (ServerCredential, ExtractGroupsForWindowsAccounts) = await
                    TransportSecurityHelpers.GetSspiCredentialAsync(_securityTokenManager, sspiTokenRequirement, token);
            }
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (IdentityVerifier == null)
            {
                IdentityVerifier = IdentityVerifier.CreateDefault();
            }

            if (ServerCredential == null)
            {
                ServerCredential = CredentialCache.DefaultNetworkCredentials;
            }
        }

        private class WindowsStreamSecurityUpgradeAcceptor : StreamSecurityUpgradeAcceptorBase
        {
            private readonly WindowsStreamSecurityUpgradeProvider _parent;
            private readonly SecurityMessageProperty _clientSecurity;
            private readonly LdapSettings _ldapSettings;

            public WindowsStreamSecurityUpgradeAcceptor(WindowsStreamSecurityUpgradeProvider parent)
                : base(FramingUpgradeString.Negotiate)
            {
                _parent = parent;
                _clientSecurity = new SecurityMessageProperty();
                _ldapSettings = parent.LdapSettings;
            }

            protected override async Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream)
            {
                // wrap stream
                NegotiateStream negotiateStream = new NegotiateStream(stream, true);

                // authenticate
                try
                {
                    await negotiateStream.AuthenticateAsServerAsync(_parent.ServerCredential, _parent.ProtectionLevel,
                        TokenImpersonationLevel.Identification);
                }
                catch (AuthenticationException exception)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(exception.Message,
                        exception));
                }
                catch (IOException ioException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(
                        SR.Format(SR.NegotiationFailedIO, ioException.Message), ioException));
                }

                SecurityMessageProperty remoteSecurity = await CreateClientSecurityAsync(negotiateStream, _parent.ExtractGroupsForWindowsAccounts);
                return (negotiateStream, remoteSecurity);
            }

            private async Task<SecurityMessageProperty> CreateClientSecurityAsync(NegotiateStream negotiateStream,
                          bool extractGroupsForWindowsAccounts)
            {
                IIdentity remoteIdentity = negotiateStream.RemoteIdentity;
                SecurityToken token;
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
                WindowsSecurityTokenAuthenticator authenticator = new WindowsSecurityTokenAuthenticator(extractGroupsForWindowsAccounts, _ldapSettings);
                if (remoteIdentity is WindowsIdentity)
                {
                    WindowsIdentity windowIdentity = (WindowsIdentity)remoteIdentity;
                    SecurityUtils.ValidateAnonymityConstraint(windowIdentity, false);
                    token = new WindowsSecurityToken(windowIdentity, SecurityUniqueId.Create().Value, windowIdentity.AuthenticationType);
                }
                else
                {
                    GenericIdentity genericIdentity = (GenericIdentity)remoteIdentity;
                    ClaimsIdentity claimsIdentity = new ClaimsIdentity(remoteIdentity);
                    token = new GenericIdentitySecurityToken(genericIdentity, SecurityUniqueId.Create().Value);
                }
                authorizationPolicies = await authenticator.ValidateTokenAsync(token);
                SecurityMessageProperty clientSecurity = new SecurityMessageProperty
                {
                    TransportToken = new SecurityTokenSpecification(token, authorizationPolicies),
                    ServiceSecurityContext = new ServiceSecurityContext(authorizationPolicies)
                };
                return clientSecurity;
            }

            public override SecurityMessageProperty GetRemoteSecurity()
            {
                if (_clientSecurity.TransportToken != null)
                {
                    return _clientSecurity;
                }
                return base.GetRemoteSecurity();
            }
        }
    }
}
