﻿using System;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF
{
    internal class DependencyInjectionInstanceProvider : IInstanceProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Type _serviceType;

        public DependencyInjectionInstanceProvider(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceProvider));
            _serviceType = serviceType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceType));
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            return _serviceProvider.GetService(_serviceType);
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            return _serviceProvider.GetService(_serviceType);
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            IDisposable dispose = instance as IDisposable;
            if (dispose != null)
                dispose.Dispose();
        }
    }
}