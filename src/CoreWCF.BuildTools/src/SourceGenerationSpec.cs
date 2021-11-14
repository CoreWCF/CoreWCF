// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal sealed class SourceGenerationSpec
    {
        public SourceGenerationSpec(List<OperationContractSpec> operationContractSpecs) => OperationContractSpecs = operationContractSpecs;
        public List<OperationContractSpec>? OperationContractSpecs { get; }
        public INamedTypeSymbol? TaskSymbol { get; set; }
        public INamedTypeSymbol? SSMOperationContractSymbol { get; set; }
        public INamedTypeSymbol? CoreWCFOperationContractSymbol { get; set; }
        public INamedTypeSymbol? GenericTaskSymbol { get; set; }
        public INamedTypeSymbol? CoreWCFInjectedSymbol { get; set; }
    }
}
