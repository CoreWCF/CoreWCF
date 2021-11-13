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
        public List<OperationContractSpec>? OperationContractSpecs { get; set; }
        public INamedTypeSymbol? TaskSymbol { get; internal set; }
        public INamedTypeSymbol? SSMOperationContractSymbol { get; internal set; }
        public INamedTypeSymbol? CoreWCFOperationContractSymbol { get; internal set; }
        public INamedTypeSymbol? GenericTaskSymbol { get; internal set; }
        public INamedTypeSymbol? CoreWCFInjectedSymbol { get; internal set; }
    }
}
