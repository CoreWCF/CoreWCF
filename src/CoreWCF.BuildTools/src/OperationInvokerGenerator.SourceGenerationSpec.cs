// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace CoreWCF.BuildTools;

public sealed partial class OperationInvokerGenerator
{
    internal readonly record struct SourceGenerationSpec(in ImmutableArray<OperationContractSpec> OperationContractSpecs)
    {
        public ImmutableArray<OperationContractSpec> OperationContractSpecs { get; } = OperationContractSpecs;

        public static readonly SourceGenerationSpec None = new();
    }
}
