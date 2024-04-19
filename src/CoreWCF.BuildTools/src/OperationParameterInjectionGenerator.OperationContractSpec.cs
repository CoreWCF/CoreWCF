// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    public sealed partial class OperationParameterInjectionGenerator
    {
        internal readonly record struct OperationContractSpec(INamedTypeSymbol? ServiceContract, INamedTypeSymbol? ServiceContractImplementation,
            IMethodSymbol? MissingOperationContract, IMethodSymbol? UserProvidedOperationContractImplementation,
            INamedTypeSymbol? HttpContextSymbol, INamedTypeSymbol? HttpRequestSymbol, INamedTypeSymbol? HttpResponseSymbol,
            AttributeData OperationContractAttributeData)
        {
            public INamedTypeSymbol? ServiceContract { get; } = ServiceContract;
            public INamedTypeSymbol? ServiceContractImplementation { get; } = ServiceContractImplementation;
            public IMethodSymbol? MissingOperationContract { get; } = MissingOperationContract;
            public IMethodSymbol? UserProvidedOperationContractImplementation { get; } = UserProvidedOperationContractImplementation;
            public INamedTypeSymbol? HttpContextSymbol { get; } = HttpContextSymbol;
            public INamedTypeSymbol? HttpRequestSymbol { get; } = HttpRequestSymbol;
            public INamedTypeSymbol? HttpResponseSymbol { get; } = HttpResponseSymbol;
            public AttributeData OperationContractAttributeData { get; } = OperationContractAttributeData;
        }
    }
}
