// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using CoreWCF;
using System.Xml;
using System.Threading.Tasks;
using System.Threading;
//using SchProtocols = CoreWCF.IdentityModel.SchProtocols;

namespace CoreWCF.Security
{
    sealed class TlsnegoTokenAuthenticator : SspiNegotiationTokenAuthenticator
    {
        SecurityTokenAuthenticator clientTokenAuthenticator;
        SecurityTokenProvider serverTokenProvider;
        X509SecurityToken serverToken;
        bool mapCertificateToWindowsAccount;

        public TlsnegoTokenAuthenticator()
            : base()
        {
            // empty
        }

        public SecurityTokenAuthenticator ClientTokenAuthenticator
        {
            get
            {
                return clientTokenAuthenticator;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                clientTokenAuthenticator = value;
            }
        }

        public SecurityTokenProvider ServerTokenProvider
        {
            get
            {
                return serverTokenProvider;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                serverTokenProvider = value;
            }
        }

        public bool MapCertificateToWindowsAccount
        {
            get
            {
                return mapCertificateToWindowsAccount;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                mapCertificateToWindowsAccount = value;
            }
        }

        X509SecurityToken ValidateX509Token(SecurityToken token)
        {
            X509SecurityToken result = token as X509SecurityToken;
            if (result == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.TokenProviderReturnedBadToken, token == null ? "<null>" : token.GetType().ToString())));
            }
            SecurityUtils.EnsureCertificateCanDoKeyExchange(result.Certificate);
            return result;
        }

        // overrides
        public override XmlDictionaryString NegotiationValueType
        {
            get 
            {
                if (StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrustFeb2005)
                {
                    return XD.TrustApr2004Dictionary.TlsnegoValueTypeUri;
                }
                else if (StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    return DXD.TrustDec2005Dictionary.TlsnegoValueTypeUri;
                }
                // Not supported
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException());
            }
        }

        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
            if (serverTokenProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.NoServerX509TokenProvider)));
            }
            await SecurityUtils.OpenTokenProviderIfRequiredAsync(serverTokenProvider, cancellationToken);
            if (clientTokenAuthenticator != null)
            {
                await SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(clientTokenAuthenticator, cancellationToken);
            }
            SecurityToken token = serverTokenProvider.GetToken(DefaultCloseTimeout);
            serverToken = ValidateX509Token(token);
            await base.OpenAsync(cancellationToken);
        }

        public override async Task CloseAsync(CancellationToken token)
        {
            if (serverTokenProvider != null)
            {
                await SecurityUtils.CloseTokenProviderIfRequiredAsync(serverTokenProvider, token);
                serverTokenProvider = null;
            }
            if (clientTokenAuthenticator != null)
            {
                await SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(clientTokenAuthenticator, token);
                clientTokenAuthenticator = null;
            }
            if (serverToken != null)
            {
                serverToken = null;
            }
            await base.CloseAsync(token);
        }

        public override void OnAbort()
        {
            if (serverTokenProvider != null)
            {
                SecurityUtils.AbortTokenProviderIfRequired(serverTokenProvider);
                serverTokenProvider = null;
            }
            if (clientTokenAuthenticator != null)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(clientTokenAuthenticator);
                clientTokenAuthenticator = null;
            }
            if (serverToken != null)
            {
                serverToken = null;
            }
            base.OnAbort();
        }

        protected override void ValidateIncomingBinaryNegotiation(BinaryNegotiation incomingNego)
        {
            // Accept both strings for WSTrustFeb2005
            if (incomingNego != null &&
                incomingNego.ValueTypeUri != NegotiationValueType.Value &&
                StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrustFeb2005)
            {
                incomingNego.Validate(DXD.TrustDec2005Dictionary.TlsnegoValueTypeUri);
            }
            else
            {
                base.ValidateIncomingBinaryNegotiation(incomingNego);
            }
        }

        protected override SspiNegotiationTokenAuthenticatorState CreateSspiState(byte[] incomingBlob, string incomingValueTypeUri)
        {
            throw new NotImplementedException();
            /*
            TlsSspiNegotiation tlsNegotiation = null;
            if (LocalAppContextSwitches.DisableUsingServicePointManagerSecurityProtocols)
            {
                tlsNegotiation = new TlsSspiNegotiation(SchProtocols.TlsServer | SchProtocols.Ssl3Server,
                serverToken.Certificate, ClientTokenAuthenticator != null);
            }
            else
            {
                var protocol = (SchProtocols)System.Net.ServicePointManager.SecurityProtocol & SchProtocols.ServerMask;
                tlsNegotiation = new TlsSspiNegotiation(protocol, serverToken.Certificate, ClientTokenAuthenticator != null);
            }
            // Echo only for TrustFeb2005 and ValueType mismatch
            if (StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrustFeb2005 && 
                NegotiationValueType.Value != incomingValueTypeUri)
            {
                tlsNegotiation.IncomingValueTypeUri = incomingValueTypeUri;
            }
            return new SspiNegotiationTokenAuthenticatorState(tlsNegotiation);
            */
        }

        protected override BinaryNegotiation GetOutgoingBinaryNegotiation(ISspiNegotiation sspiNegotiation, byte[] outgoingBlob)
        {
            throw new NotImplementedException();
            /*
            TlsSspiNegotiation tlsNegotiation = sspiNegotiation as TlsSspiNegotiation;
            // Echo only for TrustFeb2005 and ValueType mismatch
            if (StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrustFeb2005 &&
                tlsNegotiation != null &&
                tlsNegotiation.IncomingValueTypeUri != null)
            {
                return new BinaryNegotiation(tlsNegotiation.IncomingValueTypeUri, outgoingBlob);
            }
            else
            {
                return base.GetOutgoingBinaryNegotiation(sspiNegotiation, outgoingBlob);
            }*/
        }

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateSspiNegotiationAsync(ISspiNegotiation sspiNegotiation)
        {
            throw new NotImplementedException();
            /*
            TlsSspiNegotiation tlsNegotiation = (TlsSspiNegotiation)sspiNegotiation;
            if (tlsNegotiation.IsValidContext == false)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidSspiNegotiation)));
            }

            if (ClientTokenAuthenticator == null)
            {
                return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance);
            }

            X509Certificate2 clientCertificate = tlsNegotiation.RemoteCertificate;
            if (clientCertificate == null)
            {
                // isAnonymous is false. So, fail the negotiation
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityTokenValidationException(SR.Format(SR.ClientCertificateNotProvided)));
            }

            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
            if (ClientTokenAuthenticator != null)
            {
                X509SecurityToken clientToken;
                WindowsIdentity preMappedIdentity;
                if (!MapCertificateToWindowsAccount || !tlsNegotiation.TryGetContextIdentity(out preMappedIdentity))
                {
                    clientToken = new X509SecurityToken(clientCertificate);
                }
                else
                {
                    clientToken = new X509WindowsSecurityToken(clientCertificate, preMappedIdentity, preMappedIdentity.AuthenticationType, true);
                    preMappedIdentity.Dispose();
                }
                authorizationPolicies = ClientTokenAuthenticator.ValidateToken(clientToken);
                clientToken.Dispose();
            }
            else
            {
                authorizationPolicies = EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
            }
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(authorizationPolicies);
            */
        }

    }
}
