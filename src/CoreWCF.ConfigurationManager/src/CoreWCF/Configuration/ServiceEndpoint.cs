// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Configuration
{
    public class ServiceEndpoint
    {
        public string ServiceName { get; set; }
        public Uri Address { get; set; }
        public string Binding { get; set; }
        public string BindingConfiguration { get; set; }
        public string Name { get; set; }
        public string Contract { get; set; }
        public string BindingNamespace { get; set; }
    }
}
