// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    internal class ServiceConfigurationDelegateHolder
    {
        private readonly List<Action<ServiceHostBase>> _configDelegates = new List<Action<ServiceHostBase>>();

        public void AddConfigDelegate(Action<ServiceHostBase> func)
        {
            _configDelegates.Add(func);
        }

        public void Configure(ServiceHostBase host)
        {
            foreach (Action<ServiceHostBase> del in _configDelegates)
            {
                del(host);
            }
        }
    }

    internal class ServiceConfigurationDelegateHolder<TService> : ServiceConfigurationDelegateHolder
        where TService : class
    {
        
    }
}
