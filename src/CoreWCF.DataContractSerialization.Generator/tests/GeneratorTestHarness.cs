// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.DataContractSerialization.Generator.Tests
{
    /// <summary>
    /// Runs the generator over a source snippet and reports what it produced.
    /// </summary>
    /// <remarks>
    /// Drives <see cref="CSharpGeneratorDriver"/> directly rather than using
    /// Microsoft.CodeAnalysis.Testing, as CoreWCF.BuildTools does. That harness verifies generated
    /// sources by exact text match, which for a serializer body would mean pinning several hundred
    /// lines of emitted code in every one of seventy tests. Here the emitted text is asserted where
    /// it matters and the result compilation is checked for errors, so invalid generated code still
    /// fails loudly - and the whole output is pinned separately, in five snapshots, by
    /// GeneratedSourceSnapshotTests. Exact text is worth having; paying for it seventy times over is
    /// not.
    /// </remarks>
    internal sealed class GeneratorResult
    {
        internal GeneratorResult(ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compilationDiagnostics, IReadOnlyList<string> generatedSources)
        {
            GeneratorDiagnostics = generatorDiagnostics;
            CompilationDiagnostics = compilationDiagnostics;
            GeneratedSources = generatedSources;
        }

        internal ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

        internal ImmutableArray<Diagnostic> CompilationDiagnostics { get; }

        internal IReadOnlyList<string> GeneratedSources { get; }

        internal string SingleSource => GeneratedSources.Single();

        internal IEnumerable<string> DiagnosticIds => GeneratorDiagnostics.Select(d => d.Id);

        /// <summary>Errors from compiling the input together with everything the generator emitted.</summary>
        internal IEnumerable<Diagnostic> Errors =>
            CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

        /// <summary>
        /// The errors rendered for an assertion message. Without this a failure reports only that a
        /// collection was non-empty, which says nothing about what the generator got wrong.
        /// </summary>
        internal string ErrorReport =>
            string.Join(Environment.NewLine, Errors.Select(e => "  " + e.Id + ": " + e.GetMessage()));
    }

    internal static class GeneratorTestHarness
    {
        /// <summary>
        /// Runs the generator and hands back the driver, for a snapshot of everything it produced.
        /// </summary>
        /// <remarks>
        /// The assertion-based tests and the snapshots answer different questions and both are worth
        /// keeping. An assertion says why a rule exists - that a QName resolves its prefix before the
        /// element closes, say - and survives reformatting. A snapshot says what the generator
        /// actually emitted, in full, where a reviewer can read it. Neither substitutes for the
        /// other: a snapshot that changed tells you something moved but not whether it should have,
        /// and an assertion passing tells you one line is right but nothing about the other six
        /// hundred.
        /// </remarks>
        internal static GeneratorDriver RunForSnapshot(string source)
        {
            Run(source, enabled: true, out GeneratorDriver driver);
            return driver;
        }

        internal static GeneratorResult Run(string source, bool enabled = true) =>
            Run(source, enabled, out _);

        private static GeneratorResult Run(string source, bool enabled, out GeneratorDriver driver)
        {
            CSharpParseOptions parseOptions = new(LanguageVersion.CSharp11);

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: "GeneratorTests",
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
                references: MetadataReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DataContractSerializerGenerator().AsSourceGenerator() },
                parseOptions: parseOptions,
                optionsProvider: new TestOptionsProvider(enabled));

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);

            GeneratorDriverRunResult runResult = driver.GetRunResult();
            List<string> sources = runResult.Results
                .SelectMany(r => r.GeneratedSources)
                .Select(s => s.SourceText.ToString())
                .ToList();

            return new GeneratorResult(generatorDiagnostics, output.GetDiagnostics(), sources);
        }

        private static ImmutableArray<MetadataReference> MetadataReferences
        {
            get
            {
                // Everything already loaded into this test process, which is the whole framework
                // plus the two CoreWCF assemblies the generated code refers to.
                List<MetadataReference> references = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                    .ToList();

                foreach (Assembly assembly in new[]
                {
                    typeof(CoreWCF.Runtime.Serialization.AotXmlObjectSerializer).Assembly,
                    typeof(CoreWCF.DataContractSerialization.DataContractSerializerContext).Assembly,
                    typeof(System.Runtime.Serialization.DataContractAttribute).Assembly,
                    // XmlDictionaryWriter and XmlDictionaryString live here, and the emitted code is
                    // written entirely in terms of them.
                    typeof(System.Xml.XmlDictionaryWriter).Assembly,
                    // A separate assembly holding real contract types, so cross-assembly member
                    // visibility can be exercised.
                    typeof(SerializationTestTypes.BaseDCNoIsRef).Assembly
                })
                {
                    if (!references.Any(r => string.Equals(r.Display, assembly.Location, StringComparison.OrdinalIgnoreCase)))
                    {
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                }

                return references.ToImmutableArray();
            }
        }

        /// <summary>Supplies the build property the generator is gated on.</summary>
        private sealed class TestOptionsProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
        {
            public TestOptionsProvider(bool enabled) => GlobalOptions = new Options(enabled);

            public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions { get; }

            public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

            public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

            private sealed class Options : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
            {
                // Spelled out rather than referencing the generator's constant on purpose: this is
                // the name a consumer's build actually supplies, so a rename should break these
                // tests rather than silently sail through them and break consumers instead.
                private const string EnableProperty = "build_property.EnableCoreWCFDataContractSerializerGenerator";

                private readonly bool _enabled;

                public Options(bool enabled) => _enabled = enabled;

                public override bool TryGetValue(string key, out string value)
                {
                    if (key == EnableProperty)
                    {
                        value = _enabled ? "true" : "false";
                        return true;
                    }

                    value = null;
                    return false;
                }
            }
        }
    }
}
