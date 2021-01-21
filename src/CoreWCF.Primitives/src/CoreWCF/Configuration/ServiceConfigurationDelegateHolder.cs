// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    internal class ServiceConfigurationDelegateHolder<TService> where TService : class
    {
        private List<Action<ServiceHostBase>> configDelegates = new List<Action<ServiceHostBase>>();

        public void AddConfigDelegate(Action<ServiceHostBase> func)
        {
            configDelegates.Add(func);
        }

        public void Configure(ServiceHostBase host)
        {
            foreach (var del in configDelegates)
            {
                del(host);
            }
        }
    }
}
