using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.NegotiateInternal;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Security
{
    sealed class SpnegoTokenAuthenticator : SspiNegotiationTokenAuthenticator
    {
        bool extractGroupsForWindowsAccounts;
        NetworkCredential serverCredential;
        bool allowUnauthenticatedCallers;
        // SafeFreeCredentials credentialsHandle;
        NegotiateInternalState negotiateHandler;
        public SpnegoTokenAuthenticator()
            : base()
        {
            // empty
        }

        // settings        
        public bool ExtractGroupsForWindowsAccounts
        {
            get
            {
                return this.extractGroupsForWindowsAccounts;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.extractGroupsForWindowsAccounts = value;
            }
        }

        public NetworkCredential ServerCredential
        {
            get
            {
                return this.serverCredential;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.serverCredential = value;
            }
        }

        public bool AllowUnauthenticatedCallers
        {
            get
            {
                return this.allowUnauthenticatedCallers;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.allowUnauthenticatedCallers = value;
            }
        }

        // overrides
        public override XmlDictionaryString NegotiationValueType
        {
            get
            {
                return XD.TrustApr2004Dictionary.SpnegoValueTypeUri;
            }
        }

        public override Task OpenAsync(CancellationToken token)
        {
            base.OpenAsync(token);
          //  if (this.credentialsHandle == null)
          //  {
          //      this.credentialsHandle = SecurityUtils.GetCredentialsHandle("Negotiate", this.serverCredential, true);
          //  }
          if(this.negotiateHandler ==null)
                this.negotiateHandler = (NegotiateInternal.NegotiateInternalState)new NegotiateInternal.NegotiateInternalStateFactory().CreateInstance();

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

        void FreeCredentialsHandle()
        {
            //if (this.credentialsHandle != null)
            //{
            //    this.credentialsHandle.Close();
            //    this.credentialsHandle = null;
            //}
            if (this.negotiateHandler != null)
                this.negotiateHandler.Dispose();
        }

        protected override SspiNegotiationTokenAuthenticatorState CreateSspiState(byte[] incomingBlob, string incomingValueTypeUri)
        {
            ISspiNegotiation windowsNegotiation = new WindowsSspiNegotiation("Negotiate", DefaultServiceBinding, GetNegotiateState());
            return new SspiNegotiationTokenAuthenticatorState(windowsNegotiation);
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateSspiNegotiation(ISspiNegotiation sspiNegotiation)
        {
            WindowsSspiNegotiation windowsNegotiation = (WindowsSspiNegotiation)sspiNegotiation;
            if (windowsNegotiation.IsValidContext == false)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidSspiNegotiation)));
            }
           // SecurityTraceRecordHelper.TraceServiceSpnego(windowsNegotiation);
            if (this.IsClientAnonymous)
            {
                return EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
            }
            IIdentity identity = windowsNegotiation.GetIdentity();
            if (identity != null)
            {
                return GetAuthorizationPolicies(identity);
            }
            else
                throw new Exception("Identity can't be determined.");
            //using (SafeCloseHandle contextToken = windowsNegotiation.GetContextToken())
            //{
            //    WindowsIdentity windowsIdentity = new WindowsIdentity(contextToken.DangerousGetHandle(), windowsNegotiation.ProtocolName);
            //    SecurityUtils.ValidateAnonymityConstraint(windowsIdentity, this.AllowUnauthenticatedCallers);

            //    List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1);
            //    WindowsClaimSet wic = new WindowsClaimSet(windowsIdentity, windowsNegotiation.ProtocolName, this.extractGroupsForWindowsAccounts, false);
            //    policies.Add(new CoreWCF.IdentityModel.Policy.UnconditionalPolicy(wic, TimeoutHelper.Add(DateTime.UtcNow, base.ServiceTokenLifetime)));
            //    return policies.AsReadOnly();
            //}
        }

        ReadOnlyCollection<IAuthorizationPolicy> GetAuthorizationPolicies(IIdentity identity)
        {
            IIdentity remoteIdentity = identity;
            SecurityToken token;
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
            if (remoteIdentity is WindowsIdentity)
            {
                WindowsIdentity windowIdentity = (WindowsIdentity)remoteIdentity;
                Security.SecurityUtils.ValidateAnonymityConstraint(windowIdentity, this.allowUnauthenticatedCallers);
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
            return authorizationPolicies;
        }

        private NegotiateInternalState GetNegotiateState()
        {
            return (NegotiateInternalState)new NegotiateInternalStateFactory().CreateInstance();
        }
    }
}
