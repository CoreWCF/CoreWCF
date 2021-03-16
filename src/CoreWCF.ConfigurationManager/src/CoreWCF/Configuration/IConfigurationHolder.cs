// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public interface IConfigurationHolder
    {
        void AddBinding(Binding binding);
        Binding ResolveBinding(string name);
    }
}
