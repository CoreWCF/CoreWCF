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
                => _serviceScope = serviceProvider.CreateScope();

            public void Attach(InstanceContext owner)
            {
                // intentionally left blank
            }

            public void Detach(InstanceContext owner)
            {
                // intentionally left blank
            }

            public object GetService(Type serviceType) => _serviceScope.ServiceProvider.GetService(serviceType);

            public void Dispose() => _serviceScope?.Dispose();
        }

        private delegate object GetInstanceDelegate(InstanceContext instanceContext);
        private delegate void ReleaseInstanceDelegate(InstanceContext instanceContext, object instance);

        private readonly IServiceProvider _serviceProvider;
        private readonly Type _serviceType;
        private GetInstanceDelegate _getInstanceDelegate;
        private ReleaseInstanceDelegate _releaseInstanceDelegate;

        public DependencyInjectionWithLegacyFallbackInstanceProvider(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceProvider));
            _serviceType = serviceType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceType));
            _getInstanceDelegate = GetInstanceFromDIWithLegacyFallback;
            _releaseInstanceDelegate = ReleaseInstanceLegacy;
        }

        public object GetInstance(InstanceContext instanceContext)
            => GetInstance(instanceContext, null);

        public object GetInstance(InstanceContext instanceContext, Message message)
            => _getInstanceDelegate(instanceContext);

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
            => _releaseInstanceDelegate(instanceContext, instance);

        public void ReleaseInstanceLegacy(InstanceContext instanceContext, object instance)
            => (instance as IDisposable)?.Dispose();

        public void ReleaseInstanceFromDI(InstanceContext instanceContext, object instance)
            => GetScopedServiceProviderExtension(instanceContext)?.Dispose();

        private object GetInstanceFromDIWithLegacyFallback(InstanceContext instanceContext)
        {
            var instance = GetInstanceFromDI(instanceContext);
            if (instance == null) // Type not in DI
            {
                if (InvokerUtil.HasDefaultConstructor(_serviceType))
                {
                    _getInstanceDelegate = _ => InvokerUtil.GenerateCreateInstanceDelegate(_serviceType)();
                }
                else // Fallback to returning null if not in DI and no default constructor
                {
                    _getInstanceDelegate = _ => null;
                }

                return _getInstanceDelegate(instanceContext);
            }
            else
            {
                _getInstanceDelegate = GetInstanceFromDI;
                // Let the ServiceScope dispose the instance properly
                _releaseInstanceDelegate = ReleaseInstanceFromDI;
                return instance;
            }
        }

        private object GetInstanceFromDI(InstanceContext instanceContext)
        {
            ScopedServiceProviderExtension extension = GetScopedServiceProviderExtension(instanceContext);
            if (extension == null)
            {
                extension = new ScopedServiceProviderExtension(_serviceProvider);
                instanceContext.Extensions.Add(extension);
            }

            return extension.GetService(_serviceType);
        }

        private static ScopedServiceProviderExtension GetScopedServiceProviderExtension(InstanceContext instanceContext)
            => instanceContext.Extensions.SingleOrDefault(x => x.GetType() == typeof(ScopedServiceProviderExtension)) as ScopedServiceProviderExtension;
    }
}
