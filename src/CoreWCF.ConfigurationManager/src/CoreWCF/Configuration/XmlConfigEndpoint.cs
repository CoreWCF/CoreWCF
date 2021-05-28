// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class XmlConfigEndpoint : IXmlConfigEndpoint
    {
        public Uri Address { get; private set; }
        public Binding Binding { get; private set; }
        public Type Contract { get; private set; }
        public Type Service { get; private set; }

        public XmlConfigEndpoint(Type service, Type contract, Binding binding, Uri address)
        {
            Service = service;
            Contract = contract;
            Binding = binding;
            Address = address;
        }
    }
}
