// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis.Testing;

namespace Microsoft.CodeAnalysis.CSharp.Testing
{
    public class CSharpIncrementalGeneratorTest<TSourceGenerator, TVerifier> : IncrementalGeneratorTest<TVerifier>
        where TSourceGenerator : IIncrementalGenerator, new()
        where TVerifier : IVerifier, new()
    {
        private static readonly LanguageVersion DefaultLanguageVersion =
            Enum.TryParse("Default", out LanguageVersion version) ? version : LanguageVersion.CSharp6;

        protected override IEnumerable<IIncrementalGenerator> GetSourceGenerators()
            => new IIncrementalGenerator[] { new TSourceGenerator() };

        protected override string DefaultFileExt => "cs";

        public override string Language => LanguageNames.CSharp;

        protected override GeneratorDriver CreateGeneratorDriver(Project project, ImmutableArray<IIncrementalGenerator> sourceGenerators)
        {
            return CSharpGeneratorDriver.Create(
                sourceGenerators.Select(s => s.AsSourceGenerator()),
                project.AnalyzerOptions.AdditionalFiles,
                (CSharpParseOptions)project.ParseOptions!,
                project.AnalyzerOptions.AnalyzerConfigOptionsProvider);
        }

        protected override CompilationOptions CreateCompilationOptions()
            => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

        protected override ParseOptions CreateParseOptions()
            => new CSharpParseOptions(DefaultLanguageVersion, DocumentationMode.Diagnose);
    }
}
