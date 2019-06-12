using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Description;
using CoreWCF.Security;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    class WindowsStreamSecurityUpgradeProvider : StreamSecurityUpgradeProvider
    {
        EndpointIdentity _identity;
        SecurityTokenManager securityTokenManager;
        bool isClient;
        Uri listenUri;

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

        NetworkCredential ServerCredential { get; set; }

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

        class WindowsStreamSecurityUpgradeAcceptor : StreamSecurityUpgradeAcceptorBase
        {
            WindowsStreamSecurityUpgradeProvider parent;
            SecurityMessageProperty clientSecurity;

            public WindowsStreamSecurityUpgradeAcceptor(WindowsStreamSecurityUpgradeProvider parent)
                : base(FramingUpgradeString.Negotiate)
            {
                this.parent = parent;
                clientSecurity = new SecurityMessageProperty();
            }

            protected override async Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream)
            {
                // wrap stream
                NegotiateStream negotiateStream = new NegotiateStream(stream);

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

            SecurityMessageProperty CreateClientSecurity(NegotiateStream negotiateStream,
                bool extractGroupsForWindowsAccounts)
            {
                WindowsIdentity remoteIdentity = (WindowsIdentity)negotiateStream.RemoteIdentity;
                Security.SecurityUtils.ValidateAnonymityConstraint(remoteIdentity, false);
                WindowsSecurityTokenAuthenticator authenticator = new WindowsSecurityTokenAuthenticator(extractGroupsForWindowsAccounts);

                // When NegotiateStream returns a WindowsIdentity the AuthenticationType is passed in the constructor to WindowsIdentity
                // by it's internal NegoState class.  If this changes, then the call to remoteIdentity.AuthenticationType could fail if the 
                // current process token doesn't have sufficient privileges.  It is a first class exception, and caught by the CLR
                // null is returned.
                SecurityToken token = new WindowsSecurityToken(remoteIdentity, SecurityUniqueId.Create().Value, remoteIdentity.AuthenticationType);
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = authenticator.ValidateToken(token);
                clientSecurity = new SecurityMessageProperty();
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
