// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Dispatcher
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ImplementsServiceContractAttribute : Attribute
    {
        public ImplementsServiceContractAttribute(Type type)
        {
            Type = type;
        }

        public Type Type { get; }
    }
}
