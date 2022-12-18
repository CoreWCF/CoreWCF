// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Description
{
    public sealed class ServiceAuthorizationBehavior : IServiceBehavior
    {
        internal const bool DefaultImpersonateCallerForAllOperations = false;
        internal const bool DefaultImpersonateOnSerializingReply = false;
        internal const PrincipalPermissionMode DefaultPrincipalPermissionMode = PrincipalPermissionMode.UseWindowsGroups;
        private bool _impersonateCallerForAllOperations;
        private readonly bool _impersonateOnSerializingReply;
        private ReadOnlyCollection<IAuthorizationPolicy> _externalAuthorizationPolicies;
        private ServiceAuthorizationManager _serviceAuthorizationManager;
        private IServiceScopeFactory _serviceScopeFactory;
        private PrincipalPermissionMode _principalPermissionMode;
        private bool _isExternalPoliciesSet;
        private bool _isAuthorizationManagerSet;
        private bool _isReadOnly;

        public ServiceAuthorizationBehavior()
        {
            _impersonateCallerForAllOperations = DefaultImpersonateCallerForAllOperations;
            _impersonateOnSerializingReply = DefaultImpersonateOnSerializingReply;
            _principalPermissionMode = DefaultPrincipalPermissionMode;
        }

        private ServiceAuthorizationBehavior(ServiceAuthorizationBehavior other)
        {
            _impersonateCallerForAllOperations = other._impersonateCallerForAllOperations;
            _impersonateOnSerializingReply = other._impersonateOnSerializingReply;
            _principalPermissionMode = other._principalPermissionMode;
            _isExternalPoliciesSet = other._isExternalPoliciesSet;
            _isAuthorizationManagerSet = other._isAuthorizationManagerSet;

            if (other._isExternalPoliciesSet || other._isAuthorizationManagerSet)
            {
                CopyAuthorizationPoliciesAndManager(other);
            }

            _serviceScopeFactory = other._serviceScopeFactory;
            _isReadOnly = other._isReadOnly;
        }

        public ReadOnlyCollection<IAuthorizationPolicy> ExternalAuthorizationPolicies
        {
            get
            {
                return _externalAuthorizationPolicies;
            }
            set
            {
                ThrowIfImmutable();
                _isExternalPoliciesSet = true;
                _externalAuthorizationPolicies = value;
            }
        }

        public bool ShouldSerializeExternalAuthorizationPolicies()
        {
            return _isExternalPoliciesSet;
        }

        public ServiceAuthorizationManager ServiceAuthorizationManager
        {
            get
            {
                return _serviceAuthorizationManager;
            }
            set
            {
                ThrowIfImmutable();
                _isAuthorizationManagerSet = true;
                _serviceAuthorizationManager = value;
            }
        }

        public bool ShouldSerializeServiceAuthorizationManager()
        {
            return _isAuthorizationManagerSet;
        }

        [DefaultValue(DefaultPrincipalPermissionMode)]
        public PrincipalPermissionMode PrincipalPermissionMode
        {
            get
            {
                return _principalPermissionMode;
            }
            set
            {
                if (!PrincipalPermissionModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                ThrowIfImmutable();
                _principalPermissionMode = value;
            }
        }


        [DefaultValue(DefaultImpersonateCallerForAllOperations)]
        public bool ImpersonateCallerForAllOperations
        {
            get
            {
                return _impersonateCallerForAllOperations;
            }
            set
            {
                ThrowIfImmutable();
                _impersonateCallerForAllOperations = value;
            }
        }


        [DefaultValue(DefaultImpersonateOnSerializingReply)]
        public bool ImpersonateOnSerializingReply
        {
            get
            {
                return _impersonateOnSerializingReply;
            }
            set
            {
                // ThrowIfImmutable();
                // impersonateOnSerializingReply = value;
                throw new PlatformNotSupportedException();
            }
        }

        public IServiceScopeFactory ServiceScopeFactory
        {
            get => _serviceScopeFactory;
            set
            {
                ThrowIfImmutable();
                _serviceScopeFactory = value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ApplyAuthorizationPoliciesAndManager(DispatchRuntime behavior)
        {
            if (_externalAuthorizationPolicies != null)
            {
                behavior.ExternalAuthorizationPolicies = _externalAuthorizationPolicies;
            }
            if (_serviceAuthorizationManager != null)
            {
                behavior.ServiceAuthorizationManager = _serviceAuthorizationManager;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CopyAuthorizationPoliciesAndManager(ServiceAuthorizationBehavior other)
        {
            _externalAuthorizationPolicies = other._externalAuthorizationPolicies;
            _serviceAuthorizationManager = other._serviceAuthorizationManager;
        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ServiceEndpoint endpoint in serviceDescription.Endpoints)
            {
                TransportBindingElement transportBindingElement = endpoint.Binding.CreateBindingElements().Find<TransportBindingElement>();
                Fx.Assert(transportBindingElement != null, "TransportBindingElement is null");
                var behaviors = (KeyedByTypeCollection<IEndpointBehavior>)endpoint.EndpointBehaviors;
                behaviors.Add(new EndpointAuthorizationBehavior());
            }
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(description)));
            }
            if (serviceHostBase == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(serviceHostBase)));
            }

            for (int i = 0; i < serviceHostBase.ChannelDispatchers.Count; i++)
            {
                // TODO: ServiceMetadataBehavior reference needs to be put back once we have MetadataBehavior
                if (serviceHostBase.ChannelDispatchers[i] is ChannelDispatcher channelDispatcher /*&& !ServiceMetadataBehavior.IsHttpGetMetadataDispatcher(description, channelDispatcher)*/)
                {
                    foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                    {
                        DispatchRuntime behavior = endpointDispatcher.DispatchRuntime;
                        behavior.PrincipalPermissionMode = _principalPermissionMode;
                        if (!endpointDispatcher.IsSystemEndpoint)
                        {
                            behavior.ImpersonateCallerForAllOperations = _impersonateCallerForAllOperations;
                            behavior.ImpersonateOnSerializingReply = _impersonateOnSerializingReply;
                        }
                        if (_isAuthorizationManagerSet || _isExternalPoliciesSet)
                        {
                            ApplyAuthorizationPoliciesAndManager(behavior);
                        }

                        behavior.ServiceScopeFactory = _serviceScopeFactory;
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
            _isReadOnly = true;
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
