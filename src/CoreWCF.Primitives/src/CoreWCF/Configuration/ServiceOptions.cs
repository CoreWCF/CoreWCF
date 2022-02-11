// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Description;

namespace CoreWCF.Configuration
{
    public class ServiceOptions
    {
        internal ServiceOptions() { }

        public ServiceDebugBehavior DebugBehavior { get; } = new ServiceDebugBehavior();

        public ICollection<Uri> BaseAddresses { get; } = new List<Uri>();
    }

    internal class ServiceOptions<TService> : ServiceOptions where TService : class
    {
        private ServiceHostObjectModel<TService> _serviceHost;
        internal ServiceOptions(ServiceHostObjectModel<TService> serviceHost) 
        {
            _serviceHost = serviceHost;
        }

        internal void ApplyOptions(ServiceConfigurationDelegateHolder configDelegateHolder)
        {
            bool optionsDelegateProvided = configDelegateHolder.ApplyOptions(this);
            if (optionsDelegateProvided)
            {
                ApplyOptionsToServiceHost();
            }
            else
            {
                SetNoOptionsDelegateDefaults();
            }
        }

        private void ApplyOptionsToServiceHost()
        {
            _serviceHost.Description.Behaviors.Remove<ServiceDebugBehavior>();
            _serviceHost.Description.Behaviors.Add(DebugBehavior);
            if (BaseAddresses.Count > 0)
            {
                _serviceHost.InternalBaseAddresses.Clear();
                foreach (Uri baseAddress in BaseAddresses)
                {
                    _serviceHost.InternalBaseAddresses.Add(baseAddress);
                }
            }
        }

        private void SetNoOptionsDelegateDefaults()
        {
            // Enabling the debug page is a change in behavior which might take people by surprise
            // This code runs when not using the new options overload of AddService so only apps
            // which use the options overload will have the HttpHelpPageEnabled enabled by default.
            // That requires a code change so change is expected.
            DebugBehavior.HttpHelpPageEnabled = false;
        }
    }
}
