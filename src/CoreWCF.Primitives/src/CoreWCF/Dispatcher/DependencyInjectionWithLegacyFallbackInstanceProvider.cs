// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Dispatcher
{
    internal class DependencyInjectionWithLegacyFallbackInstanceProvider : IInstanceProvider
    {
        private class ScopedServiceProviderExtension : IExtension<InstanceContext>, IKeyedServiceProvider, IDisposable
        {
            private readonly IServiceScope _serviceScope;
            private IKeyedServiceProvider KeyedServiceProvider { get; }

            public ScopedServiceProviderExtension(IServiceProvider serviceProvider)
            {
                _serviceScope = serviceProvider.CreateScope();
                KeyedServiceProvider = _serviceScope.ServiceProvider as IKeyedServiceProvider;
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

            public void Dispose() => _serviceScope?.Dispose();

            public object GetKeyedService(Type serviceType, object serviceKey) => KeyedServiceProvider.GetKeyedService(serviceType, serviceKey);

            public object GetRequiredKeyedService(Type serviceType, object serviceKey) => KeyedServiceProvider.GetRequiredKeyedService(serviceType, serviceKey);
        }

        private delegate object GetInstanceDelegate(InstanceContext instanceContext);
        private delegate void ReleaseInstanceDelegate(InstanceContext instanceContext, object instance);

        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceProviderIsService _serviceProviderIsService;
        private readonly Type _serviceType;
        private GetInstanceDelegate _getInstanceDelegate;
        private ReleaseInstanceDelegate _releaseInstanceDelegate;

        public DependencyInjectionWithLegacyFallbackInstanceProvider(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceProvider));
            _serviceType = serviceType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceType));

            _serviceProviderIsService = _serviceProvider.GetRequiredService<IServiceProviderIsService>();
            _getInstanceDelegate = GetInstanceFromDIWithLegacyFallback;
            // Defaults to ReleaseInstanceLegacy
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

        public void ReleaseServiceScope(InstanceContext instanceContext, object instance)
        {
            var extension = GetScopedServiceProviderExtension(instanceContext);
            instanceContext.Extensions.Remove(extension);
            extension.Dispose();
        }

        private object GetInstanceFromDIWithLegacyFallback(InstanceContext instanceContext)
        {
            if (_serviceProviderIsService.IsService(_serviceType))
            {
                var extension = new ScopedServiceProviderExtension(_serviceProvider);
                instanceContext.Extensions.Add(extension);

                // Overwrite _getInstanceDelegate so subsequent calls pull instance from ServiceScope
                _getInstanceDelegate = GetInstanceFromDI;
                // Overwrite _releaseInstanceDelegate so subsequent calls release the ServiceScope and thus instance pulled from it.
                _releaseInstanceDelegate = ReleaseServiceScope;

                return extension.GetService(_serviceType);
            }

            if (InvokerUtil.HasDefaultConstructor(_serviceType))
            {
                CreateInstanceDelegate createInstance = InvokerUtil.GenerateCreateInstanceDelegate(_serviceType);
                _getInstanceDelegate = _ => createInstance();
            }
            else // Fallback to returning null if not in DI and no default constructor
            {
                _getInstanceDelegate = _ => null;
            }

            return _getInstanceDelegate(instanceContext);
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
            => instanceContext.Extensions.Find<ScopedServiceProviderExtension>();
    }
}
