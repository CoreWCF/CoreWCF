// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    internal class ServiceConfigurationDelegateHolder
    {
        private readonly List<Action<ServiceHostBase>> _configDelegates = new List<Action<ServiceHostBase>>();
        private readonly List<Action<ServiceOptions>> _serviceOptionsDelegates = new List<Action<ServiceOptions>>();
        internal void AddConfigDelegate(Action<ServiceHostBase> func)
        {
            _configDelegates.Add(func);
        }

        internal void Configure(ServiceHostBase host)
        {
            foreach (Action<ServiceHostBase> del in _configDelegates)
            {
                del(host);
            }
        }

        internal void AddServiceOptionsDelegate(Action<ServiceOptions> options)
        {
            _serviceOptionsDelegates.Add(options);
        }

        internal bool ApplyOptions(ServiceOptions options)
        {
            if (_serviceOptionsDelegates.Count == 0) return false;
            foreach(Action<ServiceOptions> del in _serviceOptionsDelegates)
            {
                del(options);
            }

            return true;
        }
    }

    internal class ServiceConfigurationDelegateHolder<TService> : ServiceConfigurationDelegateHolder
        where TService : class
    {
        
    }
}
