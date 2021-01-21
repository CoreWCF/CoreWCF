// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Policy;

namespace CoreWCF.Description
{
    public sealed class ServiceAuthorizationBehavior : IServiceBehavior
    {
        internal const bool DefaultImpersonateCallerForAllOperations = false;
        internal const bool DefaultImpersonateOnSerializingReply = false;
        internal const PrincipalPermissionMode DefaultPrincipalPermissionMode = PrincipalPermissionMode.UseWindowsGroups;
        private bool impersonateCallerForAllOperations;
        private bool impersonateOnSerializingReply;
        private ReadOnlyCollection<IAuthorizationPolicy> externalAuthorizationPolicies;
        private ServiceAuthorizationManager serviceAuthorizationManager;
        private PrincipalPermissionMode principalPermissionMode;
        private bool isExternalPoliciesSet;
        private bool isAuthorizationManagerSet;
        private bool isReadOnly;

        public ServiceAuthorizationBehavior()
        {
            impersonateCallerForAllOperations = DefaultImpersonateCallerForAllOperations;
            impersonateOnSerializingReply = DefaultImpersonateOnSerializingReply;
            principalPermissionMode = DefaultPrincipalPermissionMode;
        }

        private ServiceAuthorizationBehavior(ServiceAuthorizationBehavior other)
        {
            impersonateCallerForAllOperations = other.impersonateCallerForAllOperations;
            impersonateOnSerializingReply = other.impersonateOnSerializingReply;
            principalPermissionMode = other.principalPermissionMode;
            isExternalPoliciesSet = other.isExternalPoliciesSet;
            isAuthorizationManagerSet = other.isAuthorizationManagerSet;

            if (other.isExternalPoliciesSet || other.isAuthorizationManagerSet)
            {
                CopyAuthorizationPoliciesAndManager(other);
            }
            isReadOnly = other.isReadOnly;
        }

        public ReadOnlyCollection<IAuthorizationPolicy> ExternalAuthorizationPolicies
        {
            get
            {
                return externalAuthorizationPolicies;
            }
            set
            {
                ThrowIfImmutable();
                isExternalPoliciesSet = true;
                externalAuthorizationPolicies = value;
            }
        }

        public bool ShouldSerializeExternalAuthorizationPolicies()
        {
            return isExternalPoliciesSet;
        }

        public ServiceAuthorizationManager ServiceAuthorizationManager
        {
            get
            {
                return serviceAuthorizationManager;
            }
            set
            {
                ThrowIfImmutable();
                isAuthorizationManagerSet = true;
                serviceAuthorizationManager = value;
            }
        }

        public bool ShouldSerializeServiceAuthorizationManager()
        {
            return isAuthorizationManagerSet;
        }

        [DefaultValue(DefaultPrincipalPermissionMode)]
        public PrincipalPermissionMode PrincipalPermissionMode
        {
            get
            {
                return principalPermissionMode;
            }
            set
            {
                if (!PrincipalPermissionModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                ThrowIfImmutable();
                principalPermissionMode = value;
            }
        }


        [DefaultValue(DefaultImpersonateCallerForAllOperations)]
        public bool ImpersonateCallerForAllOperations
        {
            get
            {
                return impersonateCallerForAllOperations;
            }
            set
            {
                ThrowIfImmutable();
                impersonateCallerForAllOperations = value;
            }
        }


        [DefaultValue(DefaultImpersonateOnSerializingReply)]
        public bool ImpersonateOnSerializingReply
        {
            get
            {
                return impersonateOnSerializingReply;
            }
            set
            {
                // ThrowIfImmutable();
                // impersonateOnSerializingReply = value;
                throw new PlatformNotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ApplyAuthorizationPoliciesAndManager(DispatchRuntime behavior)
        {
            if (externalAuthorizationPolicies != null)
            {
                behavior.ExternalAuthorizationPolicies = externalAuthorizationPolicies;
            }
            if (serviceAuthorizationManager != null)
            {
                behavior.ServiceAuthorizationManager = serviceAuthorizationManager;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CopyAuthorizationPoliciesAndManager(ServiceAuthorizationBehavior other)
        {
            externalAuthorizationPolicies = other.externalAuthorizationPolicies;
            serviceAuthorizationManager = other.serviceAuthorizationManager;
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("description"));
            }
            if (serviceHostBase == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("serviceHostBase"));
            }

            for (int i = 0; i < serviceHostBase.ChannelDispatchers.Count; i++)
            {
                ChannelDispatcher channelDispatcher = serviceHostBase.ChannelDispatchers[i] as ChannelDispatcher;
                // TODO: ServiceMetadataBehavior reference needs to be put back once we have MetadataBehavior
                if (channelDispatcher != null /*&& !ServiceMetadataBehavior.IsHttpGetMetadataDispatcher(description, channelDispatcher)*/)
                {
                    foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                    {
                        DispatchRuntime behavior = endpointDispatcher.DispatchRuntime;
                        behavior.PrincipalPermissionMode = principalPermissionMode;
                        if (!endpointDispatcher.IsSystemEndpoint)
                        {
                            behavior.ImpersonateCallerForAllOperations = impersonateCallerForAllOperations;
                            behavior.ImpersonateOnSerializingReply = impersonateOnSerializingReply;
                        }
                        if (isAuthorizationManagerSet || isExternalPoliciesSet)
                        {
                            ApplyAuthorizationPoliciesAndManager(behavior);
                        }
                    }
                }
            }
        }

        internal ServiceAuthorizationBehavior Clone()
        {
            return new ServiceAuthorizationBehavior(this);
        }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
        }

        private void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
