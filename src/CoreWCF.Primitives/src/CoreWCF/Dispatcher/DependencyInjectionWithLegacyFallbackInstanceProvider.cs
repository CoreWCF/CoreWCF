// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Dispatcher
{
    internal class DependencyInjectionWithLegacyFallbackInstanceProvider : IInstanceProvider
    {
        class ScopedServiceProviderExtension : IExtension<InstanceContext>, IServiceProvider, IDisposable
        {
            private readonly IServiceScope _serviceScope;

            public ScopedServiceProviderExtension(IServiceProvider serviceProvider)
            {
                _serviceScope = serviceProvider.CreateScope();
            }

            public void Attach(InstanceContext owner)
            {
                // intentionally left blank
            }

            public void Detach(InstanceContext owner)
            {
                // intentionally left blank
            }

            public object GetService(Type serviceType) => _serviceScope.ServiceProvider.GetService(serviceType);

            public void Dispose()
            {
                _serviceScope?.Dispose();
            }
        }

        private delegate object CreateInstanceDelegateWithInstanceContext(InstanceContext instanceContext, Message message);

        private readonly IServiceProvider _serviceProvider;
        private readonly Type _serviceType;
        private CreateInstanceDelegateWithInstanceContext _creator;

        public DependencyInjectionWithLegacyFallbackInstanceProvider(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceProvider));
            _serviceType = serviceType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceType));
            _creator = InitialGetInstanceImpl;
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            return GetInstance(instanceContext, null);
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            return _creator(instanceContext, message);
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            if (instance is IDisposable dispose)
            {
                dispose.Dispose();
            }
        }

        private object InitialGetInstanceImpl(InstanceContext instanceContext, Message message)
        {
            var instance = GetInstanceFromDI(instanceContext, message);
            if (instance == null) // Type not in DI
            {
                if (InvokerUtil.HasDefaultConstructor(_serviceType))
                {
                    _creator = (_, __) => InvokerUtil.GenerateCreateInstanceDelegate(_serviceType)();
                }
                else // Fallback to returning null if not in DI and no default constructor
                {
                    _creator = (_, __) => null;
                }

                return _creator(instanceContext, message);
            }
            else
            {
                _creator = GetInstanceFromDI;
                return instance;
            }
        }

        private object GetInstanceFromDI(InstanceContext instanceContext, Message message)
        {
            ScopedServiceProviderExtension extension;
            extension = instanceContext.Extensions.OfType<ScopedServiceProviderExtension>().FirstOrDefault();
            if (extension == null)
            {
                extension = new ScopedServiceProviderExtension(_serviceProvider);
                instanceContext.Extensions.Add(extension);
            }

            return extension.GetService(_serviceType);
        }
    }
}
