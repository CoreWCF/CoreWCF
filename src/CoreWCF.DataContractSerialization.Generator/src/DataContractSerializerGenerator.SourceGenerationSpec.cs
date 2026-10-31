// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.DataContractSerialization.Generator;

public sealed partial class DataContractSerializerGenerator
{
    /// <summary>
    /// Everything the emitter needs for one serializer context, plus anything the parser wants to
    /// report about it.
    /// </summary>
    /// <remarks>
    /// Holds only values - no <c>ISymbol</c>, no <c>SyntaxNode</c>. That is what lets Roslyn cache
    /// this step and skip regeneration when an edit did not change any contract. CoreWCF.BuildTools'
    /// generators deliberately do the opposite, building their specs inside RegisterSourceOutput
    /// from live symbols; this one is structured to cache.
    /// </remarks>
    internal sealed record ContextSpec(
        string? Namespace,
        string Name,
        string HintName,
        EquatableArray<ContractSpec> Contracts,
        EquatableArray<EnumSpec> Enums,
        EquatableArray<DiagnosticInfo> Diagnostics) : IEquatable<ContextSpec>
    {
        /// <summary>True when the parser found a fatal problem and nothing should be emitted.</summary>
        public bool IsSuppressed { get; init; }

        public static ContextSpec Failed(EquatableArray<DiagnosticInfo> diagnostics) =>
            new(null, string.Empty, string.Empty, EquatableArray<ContractSpec>.Empty, EquatableArray<EnumSpec>.Empty, diagnostics)
            {
                IsSuppressed = true
            };
    }
}
