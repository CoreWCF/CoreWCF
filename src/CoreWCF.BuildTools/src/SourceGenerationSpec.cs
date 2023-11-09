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
        INamedTypeSymbol? GenericTaskSymbol,
        INamedTypeSymbol? CoreWCFInjectedSymbol,
        INamedTypeSymbol? MicrosoftAspNetCoreMvcFromServicesSymbol,
        INamedTypeSymbol? RemoteEndpointMessagePropertySymbol,
        INamedTypeSymbol? KafkaMessagePropertySymbol,
        INamedTypeSymbol? HttpRequestMessagePropertySymbol,
        INamedTypeSymbol? HttpResponseMessagePropertySymbol)
    {
        public ImmutableArray<OperationContractSpec> OperationContractSpecs { get; } = OperationContractSpecs;
        public INamedTypeSymbol? TaskSymbol { get; } = TaskSymbol;
        public INamedTypeSymbol? GenericTaskSymbol { get; } = GenericTaskSymbol;
        public INamedTypeSymbol? CoreWCFInjectedSymbol { get; } = CoreWCFInjectedSymbol;
        public INamedTypeSymbol? MicrosoftAspNetCoreMvcFromServicesSymbol { get; } = MicrosoftAspNetCoreMvcFromServicesSymbol;
        public INamedTypeSymbol? RemoteEndpointMessagePropertySymbol { get; } = RemoteEndpointMessagePropertySymbol;
        public INamedTypeSymbol? KafkaMessagePropertySymbol { get; } = KafkaMessagePropertySymbol;
        public INamedTypeSymbol? HttpRequestMessagePropertySymbol { get; } = HttpRequestMessagePropertySymbol;
        public INamedTypeSymbol? HttpResponseMessagePropertySymbol { get; } = HttpResponseMessagePropertySymbol;

        public static readonly SourceGenerationSpec None =
            new(in ImmutableArray<OperationContractSpec>.Empty, null, null, null, null, null, null, null, null);
    }
}
