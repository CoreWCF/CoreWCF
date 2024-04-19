// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class EnableCoreWCFOperationInvokerGeneratorAttribute : Attribute
    {
        public string Value { get; }
        public EnableCoreWCFOperationInvokerGeneratorAttribute(string value)
        {
            Value = value;
        }
    }
}
