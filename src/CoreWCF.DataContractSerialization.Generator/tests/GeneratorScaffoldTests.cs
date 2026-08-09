// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CoreWCF.DataContractSerialization.Generator.Tests
{
    /// <summary>
    /// Guards the generator assembly's shape while it is still empty.
    /// </summary>
    /// <remarks>
    /// The source generator itself arrives in the next milestone. These tests pin the invariants
    /// its future implementation must satisfy, so the constraints are enforced from the first
    /// commit rather than discovered later - and they give the project a test to run, which CI
    /// requires of anything matching CoreWCF.*.Tests.csproj.
    /// </remarks>
    public class GeneratorScaffoldTests
    {
        // Loaded by name rather than via typeof: the assembly is deliberately empty until the
        // generator itself lands, so there is no type here to anchor a reference on.
        private static Assembly GeneratorAssembly => Assembly.Load("CoreWCF.DataContractSerialization.Generator");

        private static IReadOnlyList<Type> GeneratorTypes()
        {
            return GeneratorAssembly
                .GetTypes()
                .Where(t => t.GetCustomAttribute<GeneratorAttribute>(inherit: false) != null)
                .ToList();
        }

        [Fact]
        public void GeneratorAssemblyLoads()
        {
            Assert.NotNull(GeneratorAssembly);
        }

        [Fact]
        public void EveryGeneratorIsAnIncrementalGenerator()
        {
            // CoreWCF.BuildTools still carries an ISourceGenerator variant only because it also
            // targets Roslyn 3.11. This package targets Roslyn 4.8 alone, so there is no reason to
            // implement the older, non-incremental interface.
            foreach (Type type in GeneratorTypes())
            {
                Assert.True(
                    typeof(IIncrementalGenerator).IsAssignableFrom(type),
                    type.FullName + " is marked [Generator] but does not implement IIncrementalGenerator.");
            }
        }

        [Fact]
        public void EveryGeneratorIsPublicAndConcrete()
        {
            foreach (Type type in GeneratorTypes())
            {
                Assert.True(type.IsPublic, type.FullName + " must be public to be discoverable as an analyzer.");
                Assert.False(type.IsAbstract, type.FullName + " must be concrete.");
                Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
            }
        }
    }
}
