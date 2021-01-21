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
        UserNamePasswordServiceCredential userName;
        X509CertificateInitiatorServiceCredential clientCertificate;
        X509CertificateRecipientServiceCredential serviceCertificate;
        WindowsServiceCredential windows;
        IssuedTokenServiceCredential issuedToken;
        SecureConversationServiceCredential secureConversation;
        bool useIdentityConfiguration = false;

        bool isReadOnly = false;
        bool saveBootstrapTokenInSession = true;

        ExceptionMapper exceptionMapper;

        public ServiceCredentials()
        {
            userName = new UserNamePasswordServiceCredential();
            clientCertificate = new X509CertificateInitiatorServiceCredential();
            serviceCertificate = new X509CertificateRecipientServiceCredential();
            windows = new WindowsServiceCredential();
            this.issuedToken = new IssuedTokenServiceCredential();
            this.secureConversation = new SecureConversationServiceCredential();
            exceptionMapper = new ExceptionMapper();
        }

        protected ServiceCredentials(ServiceCredentials other)
        {
            if (other == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(other));
            }
            userName = new UserNamePasswordServiceCredential(other.userName);
            clientCertificate = new X509CertificateInitiatorServiceCredential(other.clientCertificate);
            serviceCertificate = new X509CertificateRecipientServiceCredential(other.serviceCertificate);
            windows = new WindowsServiceCredential(other.windows);
            this.issuedToken = new IssuedTokenServiceCredential(other.issuedToken);
            this.secureConversation = new SecureConversationServiceCredential(other.secureConversation);
            saveBootstrapTokenInSession = other.saveBootstrapTokenInSession;
            exceptionMapper = other.exceptionMapper;
        }

        public UserNamePasswordServiceCredential UserNameAuthentication
        {
            get
            {
                return userName;
            }
        }

        public X509CertificateInitiatorServiceCredential ClientCertificate
        {
            get
            {
                return clientCertificate;
            }
        }

        public X509CertificateRecipientServiceCredential ServiceCertificate
        {
            get
            {
                return serviceCertificate;
            }
        }

        public WindowsServiceCredential WindowsAuthentication
        {
            get
            {
                return windows;
            }
        }

        public IssuedTokenServiceCredential IssuedTokenAuthentication
        {
            get
            {
                return this.issuedToken;
            }
        }

        public SecureConversationServiceCredential SecureConversationAuthentication
        {
            get
            {
                return this.secureConversation;
            }
        }

        /// <summary>
        /// Gets or sets the ExceptionMapper to be used when throwing exceptions.
        /// </summary>
        public ExceptionMapper ExceptionMapper
        {
            get
            {
                return exceptionMapper;
            }
            set
            {
                ThrowIfImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                exceptionMapper = value;
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
                ChannelDispatcher channelDispatcher = serviceHostBase.ChannelDispatchers[i] as ChannelDispatcher;
                // TODO: ServiceMetadataBehavior
                //if (channelDispatcher != null && !ServiceMetadataBehavior.IsHttpGetMetadataDispatcher(description, channelDispatcher))
                //{
                //    foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                //    {
                //        DispatchRuntime behavior = endpointDispatcher.DispatchRuntime;
                //        behavior.RequireClaimsPrincipalOnOperationContext = this.useIdentityConfiguration;
                //    }
                //}
            }
        }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
            ClientCertificate.MakeReadOnly();
            this.IssuedTokenAuthentication.MakeReadOnly();
            this.SecureConversationAuthentication.MakeReadOnly();
            ServiceCertificate.MakeReadOnly();
            UserNameAuthentication.MakeReadOnly();
            WindowsAuthentication.MakeReadOnly();
        }

        void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }

    }
}
