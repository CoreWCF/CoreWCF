// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.DataContractSerialization.Generator;

/// <summary>
/// Diagnostics reported by <see cref="DataContractSerializerGenerator"/>.
/// </summary>
/// <remarks>
/// Uses the COREWCF_04XX band; 01XX-03XX belong to CoreWCF.BuildTools. Every id here must also
/// appear in AnalyzerReleases.Unshipped.md or RS2000/RS2008 fail the build.
/// <para>
/// 0400 and 0401 fire for input that is definitely wrong - a context that cannot be generated into.
/// The rest report a fallback to the reflection-based serializer.
/// </para>
/// <para>
/// Reporting those was a deliberate change of mind. While the generated path was only ever an
/// optimization, falling back silently was the right outcome: nothing was lost but speed. It is not
/// only an optimization under Native AOT, where the switch defaults on precisely because the
/// reflection path is the broken one - there a silent fallback is a build that looks clean and
/// throws at run time. So a fallback is now a warning: visible by default, and suppressible through
/// NoWarn or EditorConfig by anyone who has accepted it.
/// </para>
/// </remarks>
internal static class DiagnosticDescriptors
{
    private const string Category = nameof(DataContractSerializerGenerator);

    internal static readonly DiagnosticDescriptor ContextMustBePartial = new(
        id: "COREWCF_0400",
        title: "DataContractSerializerContext must be partial",
        messageFormat: "Class '{0}' carries [DataContractSerializable] but is not partial, so no serializers can be generated into it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ContextMustDeriveFromBase = new(
        id: "COREWCF_0401",
        title: "DataContractSerializerContext must derive from DataContractSerializerContext",
        messageFormat: "Class '{0}' carries [DataContractSerializable] but does not derive from CoreWCF.DataContractSerialization.DataContractSerializerContext",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A contract listed in the context that no generated serializer covers.
    /// </summary>
    /// <remarks>
    /// Reported for the types the user actually listed rather than for every contract reachable from
    /// them. A nested contract that cannot be written makes its container unsupported too, so
    /// reporting both would bury the one line the user can act on under a cascade of consequences;
    /// the reason text names the underlying cause instead.
    /// </remarks>
    internal static readonly DiagnosticDescriptor FallsBackToReflection = new(
        id: "COREWCF_0403",
        title: "Contract falls back to the reflection-based serializer",
        messageFormat: "No serializer was generated for '{0}', so it falls back to the reflection-based DataContractSerializer, which needs dynamic code: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A contract the generated serializer writes but cannot read back.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="FallsBackToReflection"/> because it is half a fallback: the write
    /// path is generated and the read path is not, so a service that only returns this contract is
    /// unaffected while one that accepts it is not.
    /// </remarks>
    internal static readonly DiagnosticDescriptor ReadFallsBackToReflection = new(
        id: "COREWCF_0404",
        title: "Contract is written by generated code but not read by it",
        messageFormat: "'{0}' is written by generated code, but reading it falls back to the reflection-based DataContractSerializer, which needs dynamic code: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor TypeIsNotADataContract = new(
        id: "COREWCF_0402",
        title: "Type is not a data contract",
        messageFormat: "Type '{0}' is listed in [DataContractSerializable] but has no [DataContract] attribute; only explicit data contracts are supported",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
