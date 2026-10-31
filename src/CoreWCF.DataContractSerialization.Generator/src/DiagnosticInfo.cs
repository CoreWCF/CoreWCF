// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.DataContractSerialization.Generator;

/// <summary>
/// A diagnostic reduced to values, so it can travel through the incremental pipeline.
/// </summary>
/// <remarks>
/// <see cref="Diagnostic"/> holds a <see cref="Location"/>, which holds a <see cref="SyntaxTree"/>,
/// which roots an entire <see cref="Compilation"/>. Putting one in a cached model would both defeat
/// caching and keep compilations alive. Carrying the descriptor, the message arguments and the
/// location as plain data avoids that; the real diagnostic is built at the point of reporting.
/// </remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArguments) : IEquatable<DiagnosticInfo>
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol? symbol, params string[] messageArguments) =>
        new(descriptor,
            symbol is not null && symbol.Locations.Length > 0 ? LocationInfo.From(symbol.Locations[0]) : null,
            new EquatableArray<string>(messageArguments));

    /// <summary>Creates one from a location already reduced to values.</summary>
    /// <remarks>
    /// Used by the emitter, which sees no symbols at all - the location travels on the spec instead.
    /// </remarks>
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] messageArguments) =>
        new(descriptor, location, new EquatableArray<string>(messageArguments));

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), ToObjectArray(MessageArguments));

    private static object[] ToObjectArray(EquatableArray<string> arguments)
    {
        object[] result = new object[arguments.Count];
        for (int i = 0; i < arguments.Count; i++)
        {
            result[i] = arguments[i];
        }

        return result;
    }
}

/// <summary>A source location reduced to values. See <see cref="DiagnosticInfo"/> for why.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan) : IEquatable<LocationInfo>
{
    public static LocationInfo? From(Location location) =>
        location.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
}
