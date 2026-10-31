// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.DataContractSerialization.Generator;

/// <summary>
/// Tracks indentation depth for emitted source, used via string interpolation.
/// </summary>
/// <remarks>
/// Same idea as CoreWCF.BuildTools' Indentor, but computed rather than switched over a fixed set of
/// constants: nesting depth here is driven by the shape of the contract graph, so a hard ceiling
/// would be a latent crash rather than a useful assertion.
/// </remarks>
internal sealed class Indentor
{
    private const int SpacesPerLevel = 4;

    public int Level { get; private set; }

    public void Increment() => Level++;

    public void Decrement() => Level--;

    // Deliberately not cached in a static: generators run concurrently, and a shared mutable cache
    // would need locking to save an allocation that is noise next to building the source text.
    public override string ToString() => Level <= 0 ? string.Empty : new string(' ', Level * SpacesPerLevel);
}
