// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class DependencyInjectionWithLegacyFallbackInstanceProvider : IInstanceProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Type _serviceType;
        private CreateInstanceDelegate _creator;

        public DependencyInjectionWithLegacyFallbackInstanceProvider(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceProvider));
            _serviceType = serviceType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceType));
            _creator = InitialGetInstanceImpl;
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            return _creator();
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            return _creator();
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            if (instance is IDisposable dispose)
            {
                dispose.Dispose();
            }
        }

        private object InitialGetInstanceImpl()
        {
            var instance = GetInstanceFromDI();
            if (instance == null) // Type not in DI
            {
                if (InvokerUtil.HasDefaultConstructor(_serviceType))
                {
                    _creator = InvokerUtil.GenerateCreateInstanceDelegate(_serviceType);
                }
                else // Fallback to returning null if not in DI and no default constructor
                {
                    _creator = () => null;
                }

                return _creator();
            }
            else
            {
                _creator = GetInstanceFromDI;
                return instance;
            }
        }

        private object GetInstanceFromDI()
        {
            return _serviceProvider.GetService(_serviceType);
        }
    }
}
