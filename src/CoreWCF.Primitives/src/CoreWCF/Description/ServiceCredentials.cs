// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;

namespace CoreWCF.Description
{
    public class ServiceCredentials : SecurityCredentialsManager, IServiceBehavior
    {
        private bool _isReadOnly = false;
        private readonly bool _saveBootstrapTokenInSession = true;
        private ExceptionMapper _exceptionMapper;

        public ServiceCredentials()
        {
            UserNameAuthentication = new UserNamePasswordServiceCredential();
            ClientCertificate = new X509CertificateInitiatorServiceCredential();
            ServiceCertificate = new X509CertificateRecipientServiceCredential();
            WindowsAuthentication = new WindowsServiceCredential();
            IssuedTokenAuthentication = new IssuedTokenServiceCredential();
            SecureConversationAuthentication = new SecureConversationServiceCredential();
            _exceptionMapper = new ExceptionMapper();
        }

        protected ServiceCredentials(ServiceCredentials other)
        {
            if (other == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(other));
            }
            UserNameAuthentication = new UserNamePasswordServiceCredential(other.UserNameAuthentication);
            ClientCertificate = new X509CertificateInitiatorServiceCredential(other.ClientCertificate);
            ServiceCertificate = new X509CertificateRecipientServiceCredential(other.ServiceCertificate);
            WindowsAuthentication = new WindowsServiceCredential(other.WindowsAuthentication);
            IssuedTokenAuthentication = new IssuedTokenServiceCredential(other.IssuedTokenAuthentication);
            SecureConversationAuthentication = new SecureConversationServiceCredential(other.SecureConversationAuthentication);
            _saveBootstrapTokenInSession = other._saveBootstrapTokenInSession;
            _exceptionMapper = other._exceptionMapper;
        }

        public UserNamePasswordServiceCredential UserNameAuthentication { get; }

        public X509CertificateInitiatorServiceCredential ClientCertificate { get; }

        public X509CertificateRecipientServiceCredential ServiceCertificate { get; }

        public WindowsServiceCredential WindowsAuthentication { get; }

        public IssuedTokenServiceCredential IssuedTokenAuthentication { get; }

        public SecureConversationServiceCredential SecureConversationAuthentication { get; }

        /// <summary>
        /// Gets or sets the ExceptionMapper to be used when throwing exceptions.
        /// </summary>
        public ExceptionMapper ExceptionMapper
        {
            get
            {
                return _exceptionMapper;
            }
            set
            {
                ThrowIfImmutable();
                _exceptionMapper = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        internal static ServiceCredentials CreateDefaultCredentials()
        {
            return new ServiceCredentials();
        }

        internal override SecurityTokenManager CreateSecurityTokenManager()
        {
            return new ServiceCredentialsSecurityTokenManager(Clone());
        }

        protected virtual ServiceCredentials CloneCore()
        {
            return new ServiceCredentials(this);
        }

        public ServiceCredentials Clone()
        {
            ServiceCredentials result = CloneCore();
            if (result == null || result.GetType() != GetType())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException(SR.Format(SR.CloneNotImplementedCorrectly, GetType(), (result != null) ? result.ToString() : "null")));
            }
            return result;
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }
            // throw if bindingParameters already has a SecurityCredentialsManager
            SecurityCredentialsManager otherCredentialsManager = parameters.Find<SecurityCredentialsManager>();
            if (otherCredentialsManager != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MultipleSecurityCredentialsManagersInServiceBindingParameters, otherCredentialsManager)));
            }
            parameters.Add(this);
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            for (int i = 0; i < serviceHostBase.ChannelDispatchers.Count; i++)
            {
                // TODO: ServiceMetadataBehavior
                if (serviceHostBase.ChannelDispatchers[i] is ChannelDispatcher channelDispatcher /*&& !ServiceMetadataBehavior.IsHttpGetMetadataDispatcher(description, channelDispatcher)*/)
                {
                    foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                    {
                        DispatchRuntime behavior = endpointDispatcher.DispatchRuntime;
                        behavior.RequireClaimsPrincipalOnOperationContext = false; // _useIdentityConfiguration;
                    }
                }
            }
        }

        internal void MakeReadOnly()
        {
            _isReadOnly = true;
            ClientCertificate.MakeReadOnly();
            IssuedTokenAuthentication.MakeReadOnly();
            SecureConversationAuthentication.MakeReadOnly();
            ServiceCertificate.MakeReadOnly();
            UserNameAuthentication.MakeReadOnly();
            WindowsAuthentication.MakeReadOnly();
        }

        private void ThrowIfImmutable()
        {
            if (_isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
