// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public interface IXmlConfigEndpoint
    {
        Uri Address { get; }
        Binding Binding { get; }
        Type Contract { get; }
        Type Service { get; }
    }
}
