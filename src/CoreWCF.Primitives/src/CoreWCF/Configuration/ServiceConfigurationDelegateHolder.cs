// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    internal class ServiceConfigurationDelegateHolder<TService> where TService : class
    {
        private readonly List<Action<ServiceHostBase>> _configDelegates = new List<Action<ServiceHostBase>>();

        public void AddConfigDelegate(Action<ServiceHostBase> func)
        {
            _configDelegates.Add(func);
        }

        public void Configure(ServiceHostBase host)
        {
            foreach (var del in _configDelegates)
            {
                del(host);
            }
        }
    }
}
