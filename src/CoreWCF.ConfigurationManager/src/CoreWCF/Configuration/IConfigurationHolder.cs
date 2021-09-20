// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public interface IConfigurationHolder
    {
        ISet<ServiceEndpoint> Endpoints { get; }
        void AddBinding(Binding binding);
        void AddServiceEndpoint(string name, string serviceName, Uri address, string contract, string bindingType, string bindingName);
        Binding ResolveBinding(string bindingType, string name);
        IXmlConfigEndpoint GetXmlConfigEndpoint(ServiceEndpoint endPoint);
    }
}
