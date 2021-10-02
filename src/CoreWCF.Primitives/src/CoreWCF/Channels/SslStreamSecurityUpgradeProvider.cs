// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Channels
{
    internal class SslStreamSecurityUpgradeProvider : StreamSecurityUpgradeProvider, IStreamUpgradeChannelBindingProvider
    {
        private SecurityTokenAuthenticator _clientCertificateAuthenticator;
        private SecurityTokenProvider _serverTokenProvider;
        private EndpointIdentity _identity;
        private bool _enableChannelBinding;

        private SslStreamSecurityUpgradeProvider(IDefaultCommunicationTimeouts timeouts, SecurityTokenProvider serverTokenProvider, bool requireClientCertificate, SecurityTokenAuthenticator clientCertificateAuthenticator, string scheme, IdentityVerifier identityVerifier, SslProtocols sslProtocols)
            : base(timeouts)
        {
            _serverTokenProvider = serverTokenProvider;
            RequireClientCertificate = requireClientCertificate;
            _clientCertificateAuthenticator = clientCertificateAuthenticator;
            IdentityVerifier = identityVerifier;
            Scheme = scheme;
            SslProtocols = sslProtocols;
            ClientSecurityTokenManager = null; // Used for client but there's public api which need this and the compiler complains it's never assigned
        }

        public static SslStreamSecurityUpgradeProvider CreateServerProvider(
            SslStreamSecurityBindingElement bindingElement, BindingContext context)
        {
            SecurityCredentialsManager credentialProvider =
                context.BindingParameters.Find<SecurityCredentialsManager>();

            if (credentialProvider == null)
            {
                credentialProvider = ServiceCredentials.CreateDefaultCredentials();
            }

            Uri listenUri = TransportSecurityHelpers.GetListenUri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
            SecurityTokenManager tokenManager = credentialProvider.CreateSecurityTokenManager();

            RecipientServiceModelSecurityTokenRequirement serverCertRequirement = new RecipientServiceModelSecurityTokenRequirement
            {
                TokenType = SecurityTokenTypes.X509Certificate,
                RequireCryptographicToken = true,
                KeyUsage = SecurityKeyUsage.Exchange,
                TransportScheme = context.Binding.Scheme,
                ListenUri = listenUri
            };

            SecurityTokenProvider tokenProvider = tokenManager.CreateSecurityTokenProvider(serverCertRequirement);
            if (tokenProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ClientCredentialsUnableToCreateLocalTokenProvider, serverCertRequirement)));
            }

            SecurityTokenAuthenticator certificateAuthenticator =
                TransportSecurityHelpers.GetCertificateTokenAuthenticator(tokenManager, context.Binding.Scheme, listenUri);

            return new SslStreamSecurityUpgradeProvider(context.Binding, tokenProvider, bindingElement.RequireClientCertificate,
                certificateAuthenticator, context.Binding.Scheme, bindingElement.IdentityVerifier, bindingElement.SslProtocols);
        }

        public override EndpointIdentity Identity
        {
            get
            {
                if ((_identity == null) && (ServerCertificate != null))
                {
                    _identity = SecurityUtils.GetServiceCertificateIdentity(ServerCertificate);
                }
                return _identity;
            }
        }

        public IdentityVerifier IdentityVerifier { get; }

        public bool RequireClientCertificate { get; }

        public X509Certificate2 ServerCertificate { get; private set; }

        public SecurityTokenAuthenticator ClientCertificateAuthenticator
        {
            get
            {
                if (_clientCertificateAuthenticator == null)
                {
                    _clientCertificateAuthenticator = new X509SecurityTokenAuthenticator(X509ClientCertificateAuthentication.DefaultCertificateValidator);
                }

                return _clientCertificateAuthenticator;
            }
        }

        public SecurityTokenManager ClientSecurityTokenManager { get; }

        public string Scheme { get; }

        public SslProtocols SslProtocols { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IChannelBindingProvider) || typeof(T) == typeof(IStreamUpgradeChannelBindingProvider))
            {
                return (T)(object)this;
            }
            return base.GetProperty<T>();
        }

        ChannelBinding IStreamUpgradeChannelBindingProvider.GetChannelBinding(StreamUpgradeAcceptor upgradeAcceptor, ChannelBindingKind kind)
        {
            if (upgradeAcceptor == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(upgradeAcceptor));
            }


            if (!(upgradeAcceptor is SslStreamSecurityUpgradeAcceptor sslupgradeAcceptor))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(upgradeAcceptor), SR.Format(SR.UnsupportedUpgradeAcceptor, upgradeAcceptor.GetType()));
            }

            if (kind != ChannelBindingKind.Endpoint)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(kind), SR.Format(SR.StreamUpgradeUnsupportedChannelBindingKind, GetType(), kind));
            }

            return sslupgradeAcceptor.ChannelBinding;
        }

        void IChannelBindingProvider.EnableChannelBindingSupport()
        {
            _enableChannelBinding = true;
        }


        bool IChannelBindingProvider.IsChannelBindingSupportEnabled
        {
            get
            {
                return _enableChannelBinding;
            }
        }

        public override StreamUpgradeAcceptor CreateUpgradeAcceptor()
        {
            ThrowIfDisposedOrNotOpen();
            return new SslStreamSecurityUpgradeAcceptor(this);
        }



        protected override void OnAbort()
        {
            if (_clientCertificateAuthenticator != null)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(_clientCertificateAuthenticator);
            }
            CleanupServerCertificate();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            if (_clientCertificateAuthenticator != null)
            {
                await SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(_clientCertificateAuthenticator, token);
            }
            CleanupServerCertificate();
        }

        private void SetupServerCertificate(SecurityToken token)
        {
            if (!(token is X509SecurityToken x509Token))
            {
                SecurityUtils.AbortTokenProviderIfRequired(_serverTokenProvider);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.InvalidTokenProvided, _serverTokenProvider.GetType(), typeof(X509SecurityToken))));
            }
            ServerCertificate = new X509Certificate2(x509Token.Certificate);
        }

        private void CleanupServerCertificate()
        {
            if (ServerCertificate != null)
            {
                SecurityUtils.ResetCertificate(ServerCertificate);
                ServerCertificate = null;
            }
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            await SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(ClientCertificateAuthenticator, token);

            if (_serverTokenProvider != null)
            {
                await SecurityUtils.OpenTokenProviderIfRequiredAsync(_serverTokenProvider, token);
                // TODO: Solve issue with GetToken/GetTokenAsync needing timeouts and there is only a token available
                SecurityToken securityToken = await _serverTokenProvider.GetTokenAsync(token);
                SetupServerCertificate(securityToken);
                await SecurityUtils.CloseTokenProviderIfRequiredAsync(_serverTokenProvider, token);
                _serverTokenProvider = null;
            }
        }
    }

    internal class SslStreamSecurityUpgradeAcceptor : StreamSecurityUpgradeAcceptorBase
    {
        private readonly SslStreamSecurityUpgradeProvider _parent;
        private SecurityMessageProperty _clientSecurity;

        // for audit
        private X509Certificate2 _clientCertificate = null;
        private ChannelBinding _channelBindingToken;

        public SslStreamSecurityUpgradeAcceptor(SslStreamSecurityUpgradeProvider parent)
            : base(FramingUpgradeString.SslOrTls)
        {
            _parent = parent;
            _clientSecurity = new SecurityMessageProperty();
        }

        internal ChannelBinding ChannelBinding
        {
            get
            {
                Fx.Assert(IsChannelBindingSupportEnabled, "A request for the ChannelBinding is not permitted without enabling ChannelBinding first (through the IChannelBindingProvider interface)");
                return _channelBindingToken;
            }
        }

        internal bool IsChannelBindingSupportEnabled
        {
            get
            {
                return ((IChannelBindingProvider)_parent).IsChannelBindingSupportEnabled;
            }
        }

        protected override async Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream)
        {
            var sslStream = new SslStream(stream, false, ValidateRemoteCertificate);

            try
            {
                await sslStream.AuthenticateAsServerAsync(_parent.ServerCertificate, _parent.RequireClientCertificate,
                    _parent.SslProtocols, false);
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

            SecurityMessageProperty remoteSecurity = _clientSecurity;

            if (IsChannelBindingSupportEnabled)
            {
                _channelBindingToken = ChannelBindingUtility.GetToken(sslStream);
            }

            return (sslStream, remoteSecurity);
        }

        // callback from schannel
        private bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (_parent.RequireClientCertificate)
            {
                if (certificate == null)
                {
                    return false;
                }
                // Note: add ref to handle since the caller will reset the cert after the callback return.
                X509Certificate2 certificate2 = new X509Certificate2(certificate);
                _clientCertificate = certificate2;
                try
                {
                    SecurityToken token = new X509SecurityToken(certificate2, true);
                    ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
                    var validationValueTask = _parent.ClientCertificateAuthenticator.ValidateTokenAsync(token);
                    authorizationPolicies = validationValueTask.IsCompleted
                        ? validationValueTask.Result
                        : validationValueTask.AsTask().GetAwaiter().GetResult();

                    _clientSecurity = new SecurityMessageProperty
                    {
                        TransportToken = new SecurityTokenSpecification(token, authorizationPolicies),
                        ServiceSecurityContext = new ServiceSecurityContext(authorizationPolicies)
                    };
                }
                catch (SecurityTokenException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    return false;
                }
            }
            return true;
        }

        public override SecurityMessageProperty GetRemoteSecurity()
        {
            if (_clientSecurity.TransportToken != null)
            {
                return _clientSecurity;
            }
            if (_clientCertificate != null)
            {
                return _clientSecurity;
            }
            return base.GetRemoteSecurity();
        }
    }
}
