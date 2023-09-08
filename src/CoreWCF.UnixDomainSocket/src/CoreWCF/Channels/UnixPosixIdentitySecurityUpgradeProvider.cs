// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using CoreWCF.Description;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using CoreWCF.UnixDomainSocket.Security;
using Microsoft.AspNetCore.Connections.Features;

namespace CoreWCF.Channels
{
    internal class UnixPosixIdentitySecurityUpgradeProvider : StreamSecurityUpgradeProvider
    {
        private EndpointIdentity _identity;

        public UnixPosixIdentitySecurityUpgradeProvider(UnixPosixIdentityBindingElement bindingElement, BindingContext context)
            : base(context.Binding)
        {
            Scheme = context.Binding.Scheme;
            SecurityCredentialsManager credentialProvider = context.BindingParameters.Find<SecurityCredentialsManager>();
            if (credentialProvider == null)
            {
                credentialProvider = new ServiceCredentials();
            }
        }

        public string Scheme { get; }

        public override EndpointIdentity Identity
        {
            get
            {
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

        private NetworkCredential ServerCredential { get; set; }


        public override StreamUpgradeAcceptor CreateUpgradeAcceptor()
        {
            ThrowIfDisposedOrNotOpen();
            return new UnixPosixIdentitySecurityUpgradeAcceptor(this);
        }

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override  Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (ServerCredential == null)
            {
                ServerCredential = CredentialCache.DefaultNetworkCredentials;
            }
        }

        internal class UnixPosixIdentitySecurityUpgradeAcceptor : StreamSecurityUpgradeAcceptor
        {
            private readonly UnixPosixIdentitySecurityUpgradeProvider _parent;
            private SecurityMessageProperty _remoteSecurity;
            private bool _securityUpgraded;
            private Socket _socket;
            private readonly string _upgradeString;
            public UnixPosixIdentitySecurityUpgradeAcceptor(UnixPosixIdentitySecurityUpgradeProvider parent)
            {
                _parent = parent;
                _remoteSecurity = new SecurityMessageProperty();
                _upgradeString = "application/unixposix";
            }


            public override async Task<Stream> AcceptUpgradeAsync(Stream stream)
            {
                FramingConnection conn = this.Features.Get<FramingConnection>();
                _socket = conn.ConnectionFeatures.Get<IConnectionSocketFeature>().Socket;
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
                }

                (stream, _remoteSecurity) = await OnAcceptUpgradeAsync(stream);
                _securityUpgraded = true;
                return stream;
            }

            public override bool CanUpgrade(string contentType)
            {
                if (_securityUpgraded)
                {
                    return false;
                }

                return (contentType == _upgradeString);
            }

            public override SecurityMessageProperty GetRemoteSecurity()
            {
                return _remoteSecurity;
            }

            protected async Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream)
            {

                SecurityMessageProperty remoteSecurity = await CreateClientSecurityAsync();
                return (stream, remoteSecurity);
            }

            private async Task<SecurityMessageProperty> CreateClientSecurityAsync()
            {

                if(!_socket.TryGetCredentials(out uint processId, out uint userId, out uint groupId))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.AuthenticationFailed));
                }
                GenericIdentity genericIdentity = null;
                if (userId > 0)
                {
                    UserInfo userInfo = NativeSysCall.GetUserInfo(userId);
                    GroupInfo groupInfo = NativeSysCall.GetGroupInfo(groupId);
                    if (userInfo == null || groupInfo == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.AuthenticationFailed));
                    }
                    genericIdentity = new GenericIdentity(userInfo.Name);
                    ClaimsIdentity claimsIdentity = new ClaimsIdentity(genericIdentity);
                    //should we add member of (Group) ?
                    SecurityUtils.AddPosixClaims(claimsIdentity, groupInfo.Name, groupInfo.Id, processId);
                }
                else if(processId>0)
                {
                    genericIdentity = new GenericIdentity(processId.ToString());
                    ClaimsIdentity claimsIdentity = new ClaimsIdentity(genericIdentity);
                    SecurityUtils.AddProcessIdClaim(claimsIdentity, processId);
                }
                SecurityToken token = new GenericIdentitySecurityToken(genericIdentity, SecurityUniqueId.Create().Value);
                WindowsSecurityTokenAuthenticator authenticator = new WindowsSecurityTokenAuthenticator();
                ReadOnlyCollection<IAuthorizationPolicy>  authorizationPolicies = await authenticator.ValidateTokenAsync(token);
                SecurityMessageProperty clientSecurity = new SecurityMessageProperty
                {
                    TransportToken = new SecurityTokenSpecification(token, authorizationPolicies),
                    ServiceSecurityContext = new ServiceSecurityContext(authorizationPolicies)
                };
                return clientSecurity;
            }
        }
    }
}
