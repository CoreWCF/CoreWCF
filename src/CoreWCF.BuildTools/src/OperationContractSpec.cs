// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal sealed class OperationContractSpec
    {
        public INamedTypeSymbol? ServiceContract { get; set; }
        public INamedTypeSymbol? ServiceContractImplementation { get; set; }
        public IMethodSymbol? OperationContract { get; set; }
        public IMethodSymbol? OperationContractImplementationCandidate { get; set; }
    }
}
