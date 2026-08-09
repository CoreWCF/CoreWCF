// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using VerifyTests;

namespace CoreWCF.DataContractSerialization.Generator.Tests
{
    internal static class ModuleInitializer
    {
        /// <summary>
        /// Teaches Verify how to render a <c>GeneratorDriver</c> run, and where to keep the results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Snapshots live in a folder of their own rather than beside the test file. There are only a
        /// handful of them and each is hundreds of lines of generated code; interleaved with the
        /// source they would bury it.
        /// </para>
        /// <para>
        /// One snapshot serves all three target frameworks. Verify names the <c>.received.</c> file
        /// per framework so concurrent runs cannot collide, but they are all compared against the
        /// same <c>.verified.</c> file - which is the right arrangement here, because the generator's
        /// output is a function of the contract it is given rather than of the runtime the test host
        /// happens to be on. It also means every run re-checks that claim: three frameworks
        /// disagreeing about a snapshot is exactly the signal worth having.
        /// </para>
        /// </remarks>
        [ModuleInitializer]
        internal static void Initialize()
        {
            VerifySourceGenerators.Initialize();
        }
    }
}
