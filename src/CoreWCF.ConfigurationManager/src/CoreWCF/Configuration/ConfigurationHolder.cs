// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class ConfigurationHolder : IConfigurationHolder
    {
        private readonly ConcurrentDictionary<string, Binding> _bindings = new ConcurrentDictionary<string, Binding>();
        public void AddBinding(Binding binding)
        {
            _bindings.TryAdd(binding.Name, binding);
        }

        public Binding ResolveBinding(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(name);
            }

            if (_bindings.TryGetValue(name, out var binding))
            {
                return binding;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new NotFoundBindingException());
        }
    }
}
