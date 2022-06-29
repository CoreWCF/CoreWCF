// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.NegotiateInternal;

namespace CoreWCF.Security
{
    internal sealed class SpnegoTokenAuthenticator : SspiNegotiationTokenAuthenticator
    {
        private bool _extractGroupsForWindowsAccounts;
        private NetworkCredential _serverCredential;
        private bool _allowUnauthenticatedCallers;
        private LdapSettings _ldapSettings;

        // SafeFreeCredentials credentialsHandle;
        private NegotiateInternalState _negotiateHandler;
        public SpnegoTokenAuthenticator()
            : base()
        {
            // empty
        }

        // settings        
        public bool ExtractGroupsForWindowsAccounts
        {
            get => _extractGroupsForWindowsAccounts;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _extractGroupsForWindowsAccounts = value;
            }
        }

        public NetworkCredential ServerCredential
        {
            get => _serverCredential;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _serverCredential = value;
            }
        }

        public LdapSettings LdapSettings
        {
            get => _ldapSettings;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _ldapSettings = value;
            }
        }

        public bool AllowUnauthenticatedCallers
        {
            get => _allowUnauthenticatedCallers;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _allowUnauthenticatedCallers = value;
            }
        }

        // overrides
        public override XmlDictionaryString NegotiationValueType => XD.TrustApr2004Dictionary.SpnegoValueTypeUri;

        public override Task OpenAsync(CancellationToken token)
        {
            base.OpenAsync(token);
            if (_negotiateHandler == null)
            {
                _negotiateHandler = (NegotiateInternal.NegotiateInternalState)new NegotiateInternal.NegotiateInternalStateFactory().CreateInstance();
            }

            return Task.CompletedTask;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            base.CloseAsync(token);
            FreeCredentialsHandle();
            return Task.CompletedTask;
        }

        public override void OnAbort()
        {
            try
            {
                base.OnAbort();
            }
            finally
            {
                FreeCredentialsHandle();
            }
        }

        private void FreeCredentialsHandle()
        {
            if (_negotiateHandler != null)
            {
                _negotiateHandler.Dispose();
            }
        }

        protected override SspiNegotiationTokenAuthenticatorState CreateSspiState(byte[] incomingBlob, string incomingValueTypeUri)
        {
            ISspiNegotiation windowsNegotiation = new WindowsSspiNegotiation("Negotiate", GetNegotiateState());
            return new SspiNegotiationTokenAuthenticatorState(windowsNegotiation);
        }

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateSspiNegotiationAsync(ISspiNegotiation sspiNegotiation)
        {
            WindowsSspiNegotiation windowsNegotiation = (WindowsSspiNegotiation)sspiNegotiation;
            if (windowsNegotiation.IsValidContext == false)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidSspiNegotiation)));
            }
            // SecurityTraceRecordHelper.TraceServiceSpnego(windowsNegotiation);
            if (IsClientAnonymous)
            {
                return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance);
            }
            IIdentity identity = windowsNegotiation.GetIdentity();
            if (identity != null)
            {
                return GetAuthorizationPoliciesAsync(identity);
            }
            else
            {
                throw new Exception("Identity can't be determined.");
            }
        }

        private ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> GetAuthorizationPoliciesAsync(IIdentity identity)
        {
            IIdentity remoteIdentity = identity;
            SecurityToken token;
            WindowsSecurityTokenAuthenticator authenticator = new WindowsSecurityTokenAuthenticator(_extractGroupsForWindowsAccounts, _ldapSettings);
            if (remoteIdentity is WindowsIdentity)
            {
                WindowsIdentity windowIdentity = (WindowsIdentity)remoteIdentity;
                SecurityUtils.ValidateAnonymityConstraint(windowIdentity, false);
                token = new WindowsSecurityToken(windowIdentity, SecurityUniqueId.Create().Value, windowIdentity.AuthenticationType);
            }
            else
            {
                token = new GenericIdentitySecurityToken((GenericIdentity)remoteIdentity, SecurityUniqueId.Create().Value);
            }
            return authenticator.ValidateTokenAsync(token);
        }

        private NegotiateInternalState GetNegotiateState() => (NegotiateInternalState)new NegotiateInternalStateFactory().CreateInstance();
    }
}
