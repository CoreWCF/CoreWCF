// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools;

public sealed partial class OperationInvokerGenerator
{
    internal readonly record struct OperationContractSpec(IMethodSymbol? Method)
    {
        public IMethodSymbol? Method { get; } = Method;
    }
}
