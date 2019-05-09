using Microsoft.IdentityModel.Policy;
using Microsoft.IdentityModel.Selectors;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Description;
using Microsoft.ServiceModel.Security;
using Microsoft.ServiceModel.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    class SslStreamSecurityUpgradeProvider : StreamSecurityUpgradeProvider, IStreamUpgradeChannelBindingProvider
    {
        SecurityTokenAuthenticator clientCertificateAuthenticator;
        SecurityTokenProvider serverTokenProvider;
        EndpointIdentity identity;
        IdentityVerifier identityVerifier;
        X509Certificate2 serverCertificate;
        bool requireClientCertificate;
        string scheme;
        bool enableChannelBinding;
        SslProtocols sslProtocols;
        SecurityTokenManager clientSecurityTokenManager;

        SslStreamSecurityUpgradeProvider(IDefaultCommunicationTimeouts timeouts, SecurityTokenProvider serverTokenProvider, bool requireClientCertificate, SecurityTokenAuthenticator clientCertificateAuthenticator, string scheme, IdentityVerifier identityVerifier, SslProtocols sslProtocols)
            : base(timeouts)
        {
            this.serverTokenProvider = serverTokenProvider;
            this.requireClientCertificate = requireClientCertificate;
            this.clientCertificateAuthenticator = clientCertificateAuthenticator;
            this.identityVerifier = identityVerifier;
            this.scheme = scheme;
            this.sslProtocols = sslProtocols;
            clientSecurityTokenManager = null; // Used for client but there's public api which need this and the compiler complains it's never assigned
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

            RecipientServiceModelSecurityTokenRequirement serverCertRequirement = new RecipientServiceModelSecurityTokenRequirement();
            serverCertRequirement.TokenType = SecurityTokenTypes.X509Certificate;
            serverCertRequirement.RequireCryptographicToken = true;
            serverCertRequirement.KeyUsage = SecurityKeyUsage.Exchange;
            serverCertRequirement.TransportScheme = context.Binding.Scheme;
            serverCertRequirement.ListenUri = listenUri;

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
                if ((identity == null) && (serverCertificate != null))
                {
                    identity = SecurityUtils.GetServiceCertificateIdentity(serverCertificate);
                }
                return identity;
            }
        }

        public IdentityVerifier IdentityVerifier
        {
            get
            {
                return identityVerifier;
            }
        }

        public bool RequireClientCertificate
        {
            get
            {
                return requireClientCertificate;
            }
        }

        public X509Certificate2 ServerCertificate
        {
            get
            {
                return serverCertificate;
            }
        }

        public SecurityTokenAuthenticator ClientCertificateAuthenticator
        {
            get
            {
                if (clientCertificateAuthenticator == null)
                {
                    clientCertificateAuthenticator = new X509SecurityTokenAuthenticator(X509ClientCertificateAuthentication.DefaultCertificateValidator);
                }

                return clientCertificateAuthenticator;
            }
        }

        public SecurityTokenManager ClientSecurityTokenManager
        {
            get
            {
                return clientSecurityTokenManager;
            }
        }

        public string Scheme
        {
            get { return scheme; }
        }

        public SslProtocols SslProtocols
        {
            get { return sslProtocols; }
        }

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("upgradeAcceptor");
            }

            SslStreamSecurityUpgradeAcceptor sslupgradeAcceptor = upgradeAcceptor as SslStreamSecurityUpgradeAcceptor;

            if (sslupgradeAcceptor == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("upgradeAcceptor", SR.Format(SR.UnsupportedUpgradeAcceptor, upgradeAcceptor.GetType()));
            }

            if (kind != ChannelBindingKind.Endpoint)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("kind", SR.Format(SR.StreamUpgradeUnsupportedChannelBindingKind, GetType(), kind));
            }

            return sslupgradeAcceptor.ChannelBinding;
        }

        void IChannelBindingProvider.EnableChannelBindingSupport()
        {
            enableChannelBinding = true;
        }


        bool IChannelBindingProvider.IsChannelBindingSupportEnabled
        {
            get
            {
                return enableChannelBinding;
            }
        }

        public override StreamUpgradeAcceptor CreateUpgradeAcceptor()
        {
            ThrowIfDisposedOrNotOpen();
            return new SslStreamSecurityUpgradeAcceptor(this);
        }



        protected override void OnAbort()
        {
            if (clientCertificateAuthenticator != null)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(clientCertificateAuthenticator);
            }
            CleanupServerCertificate();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            if (clientCertificateAuthenticator != null)
            {
                await SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(clientCertificateAuthenticator, token);
            }
            CleanupServerCertificate();
        }

        void SetupServerCertificate(SecurityToken token)
        {
            X509SecurityToken x509Token = token as X509SecurityToken;
            if (x509Token == null)
            {
                SecurityUtils.AbortTokenProviderIfRequired(serverTokenProvider);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.InvalidTokenProvided, serverTokenProvider.GetType(), typeof(X509SecurityToken))));
            }
            serverCertificate = new X509Certificate2(x509Token.Certificate);
        }

        void CleanupServerCertificate()
        {
            if (serverCertificate != null)
            {
                SecurityUtils.ResetCertificate(serverCertificate);
                serverCertificate = null;
            }
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            await SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(ClientCertificateAuthenticator, token);

            if (serverTokenProvider != null)
            {
                await SecurityUtils.OpenTokenProviderIfRequiredAsync(serverTokenProvider, token);
                // TODO: Solve issue with GetToken/GetTokenAsync needing timeouts and there is only a token available
                SecurityToken securityToken = await serverTokenProvider.GetTokenAsync(token);
                SetupServerCertificate(securityToken);
                await SecurityUtils.CloseTokenProviderIfRequiredAsync(serverTokenProvider, token);
                serverTokenProvider = null;
            }
        }
    }

    class SslStreamSecurityUpgradeAcceptor : StreamSecurityUpgradeAcceptorBase
    {
        SslStreamSecurityUpgradeProvider parent;
        SecurityMessageProperty clientSecurity;
        // for audit
        X509Certificate2 clientCertificate = null;
        ChannelBinding channelBindingToken;

        public SslStreamSecurityUpgradeAcceptor(SslStreamSecurityUpgradeProvider parent)
            : base(FramingUpgradeString.SslOrTls)
        {
            this.parent = parent;
            clientSecurity = new SecurityMessageProperty();
        }

        internal ChannelBinding ChannelBinding
        {
            get
            {
                Fx.Assert(IsChannelBindingSupportEnabled, "A request for the ChannelBinding is not permitted without enabling ChannelBinding first (through the IChannelBindingProvider interface)");
                return channelBindingToken;
            }
        }

        internal bool IsChannelBindingSupportEnabled
        {
            get
            {
                return ((IChannelBindingProvider)parent).IsChannelBindingSupportEnabled;
            }
        }

        protected override async Task<(Stream,SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream)
        {
            var sslStream = new SslStream(stream, false, ValidateRemoteCertificate);

            try
            {
                await sslStream.AuthenticateAsServerAsync(parent.ServerCertificate, parent.RequireClientCertificate,
                    parent.SslProtocols, false);
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

            SecurityMessageProperty remoteSecurity = clientSecurity;

            if (IsChannelBindingSupportEnabled)
            {
                channelBindingToken = ChannelBindingUtility.GetToken(sslStream);
            }

            return (sslStream, remoteSecurity);
        }

        // callback from schannel
        bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (parent.RequireClientCertificate)
            {
                if (certificate == null)
                {
                    return false;
                }
                // Note: add ref to handle since the caller will reset the cert after the callback return.
                X509Certificate2 certificate2 = new X509Certificate2(certificate);
                clientCertificate = certificate2;
                try
                {
                    SecurityToken token = new X509SecurityToken(certificate2, false);
                    ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = parent.ClientCertificateAuthenticator.ValidateToken(token);
                    clientSecurity = new SecurityMessageProperty();
                    clientSecurity.TransportToken = new SecurityTokenSpecification(token, authorizationPolicies);
                    clientSecurity.ServiceSecurityContext = new ServiceSecurityContext(authorizationPolicies);
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
            if (clientSecurity.TransportToken != null)
            {
                return clientSecurity;
            }
            if (clientCertificate != null)
            {
                SecurityToken token = new X509SecurityToken(clientCertificate);
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = SecurityUtils.NonValidatingX509Authenticator.ValidateToken(token);
                clientSecurity = new SecurityMessageProperty();
                clientSecurity.TransportToken = new SecurityTokenSpecification(token, authorizationPolicies);
                clientSecurity.ServiceSecurityContext = new ServiceSecurityContext(authorizationPolicies);
                return clientSecurity;
            }
            return base.GetRemoteSecurity();
        }
    }
}
