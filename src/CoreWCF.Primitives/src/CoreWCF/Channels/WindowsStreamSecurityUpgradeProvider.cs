// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
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
        private readonly SecurityTokenManager securityTokenManager;
        private readonly bool isClient;
        private readonly Uri listenUri;

        public WindowsStreamSecurityUpgradeProvider(WindowsStreamSecurityBindingElement bindingElement,
            BindingContext context, bool isClient)
            : base(context.Binding)
        {
            ExtractGroupsForWindowsAccounts = TransportDefaults.ExtractGroupsForWindowsAccounts;
            ProtectionLevel = bindingElement.ProtectionLevel;
            Scheme = context.Binding.Scheme;
            this.isClient = isClient;
            listenUri = TransportSecurityHelpers.GetListenUri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);

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


            securityTokenManager = credentialProvider.CreateSecurityTokenManager();
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
                                _identity = Security.SecurityUtils.CreateWindowsIdentity(ServerCredential);
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
            if (!isClient)
            {
                SecurityTokenRequirement sspiTokenRequirement = TransportSecurityHelpers.CreateSspiTokenRequirement(Scheme, listenUri);
                (ServerCredential, ExtractGroupsForWindowsAccounts) = await
                    TransportSecurityHelpers.GetSspiCredentialAsync(securityTokenManager, sspiTokenRequirement, token);
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
            private readonly WindowsStreamSecurityUpgradeProvider parent;
            private readonly SecurityMessageProperty clientSecurity;

            public WindowsStreamSecurityUpgradeAcceptor(WindowsStreamSecurityUpgradeProvider parent)
                : base(FramingUpgradeString.Negotiate)
            {
                this.parent = parent;
                clientSecurity = new SecurityMessageProperty();
            }

            protected override async Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream)
            {
                // wrap stream
                NegotiateStream negotiateStream = new NegotiateStream(stream, true);

                // authenticate
                try
                {
                    await negotiateStream.AuthenticateAsServerAsync(parent.ServerCredential, parent.ProtectionLevel,
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

                SecurityMessageProperty remoteSecurity = CreateClientSecurity(negotiateStream, parent.ExtractGroupsForWindowsAccounts);
                return (negotiateStream, remoteSecurity);
            }

            private SecurityMessageProperty CreateClientSecurity(NegotiateStream negotiateStream,
                          bool extractGroupsForWindowsAccounts)
            {
                IIdentity remoteIdentity = negotiateStream.RemoteIdentity;
                SecurityToken token;
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
                if (remoteIdentity is WindowsIdentity)
                {
                    WindowsIdentity windowIdentity = (WindowsIdentity)remoteIdentity;
                    Security.SecurityUtils.ValidateAnonymityConstraint(windowIdentity, false);
                    WindowsSecurityTokenAuthenticator authenticator = new WindowsSecurityTokenAuthenticator(extractGroupsForWindowsAccounts);
                    token = new WindowsSecurityToken(windowIdentity, SecurityUniqueId.Create().Value, windowIdentity.AuthenticationType);
                    authorizationPolicies = authenticator.ValidateToken(token);
                }
                else
                {
                    token = new GenericSecurityToken(remoteIdentity.Name, SecurityUniqueId.Create().Value);
                    GenericSecurityTokenAuthenticator authenticator = new GenericSecurityTokenAuthenticator();
                    authorizationPolicies = authenticator.ValidateToken(token);
                }
                SecurityMessageProperty clientSecurity = new SecurityMessageProperty();
                clientSecurity.TransportToken = new SecurityTokenSpecification(token, authorizationPolicies);
                clientSecurity.ServiceSecurityContext = new ServiceSecurityContext(authorizationPolicies);
                return clientSecurity;
            }

            public override SecurityMessageProperty GetRemoteSecurity()
            {
                if (clientSecurity.TransportToken != null)
                {
                    return clientSecurity;
                }
                return base.GetRemoteSecurity();
            }
        }
    }
}
