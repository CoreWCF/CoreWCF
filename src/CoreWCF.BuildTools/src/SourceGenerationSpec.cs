// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal readonly record struct SourceGenerationSpec(in ImmutableArray<OperationContractSpec> OperationContractSpecs,
        INamedTypeSymbol? TaskSymbol,
        INamedTypeSymbol? SSMOperationContractSymbol,
        INamedTypeSymbol? CoreWCFOperationContractSymbol,
        INamedTypeSymbol? GenericTaskSymbol,
        INamedTypeSymbol? CoreWCFInjectedSymbol,
        INamedTypeSymbol? MicrosoftAspNetCoreMvcFromServicesSymbol)
    {
        public ImmutableArray<OperationContractSpec> OperationContractSpecs { get; } = OperationContractSpecs;
        public INamedTypeSymbol? TaskSymbol { get; } = TaskSymbol;
        public INamedTypeSymbol? SSMOperationContractSymbol { get; } = SSMOperationContractSymbol;
        public INamedTypeSymbol? CoreWCFOperationContractSymbol { get; } = CoreWCFOperationContractSymbol;
        public INamedTypeSymbol? GenericTaskSymbol { get; } = GenericTaskSymbol;
        public INamedTypeSymbol? CoreWCFInjectedSymbol { get; } = CoreWCFInjectedSymbol;

        public INamedTypeSymbol? MicrosoftAspNetCoreMvcFromServicesSymbol { get; } = MicrosoftAspNetCoreMvcFromServicesSymbol;

        public static readonly SourceGenerationSpec None =
            new(in ImmutableArray<OperationContractSpec>.Empty, null, null, null, null, null, null);
    }
}
