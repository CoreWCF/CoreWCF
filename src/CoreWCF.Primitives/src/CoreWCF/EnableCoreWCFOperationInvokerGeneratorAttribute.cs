// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class EnableCoreWCFOperationInvokerGeneratorAttribute : Attribute
    {
        public EnableCoreWCFOperationInvokerGeneratorAttribute(bool value)
        {
            Value = value;
        }

        public bool Value { get; }
    }
}
