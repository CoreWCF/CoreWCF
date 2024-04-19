// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using static CoreWCF.BuildTools.OperationParameterInjectionGenerator;

namespace CoreWCF.BuildTools
{
    [Generator]
    public sealed partial class OperationInvokerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<MethodDeclarationSyntax?> methodDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
                          predicate: static (token, _) => Parser.IsSyntaxTargetForGeneration(token),
                          transform: static (s, _) => Parser.GetSemanticTargetForGeneration(s))
                          .Where(static c => c is not null);

            IncrementalValueProvider<(AnalyzerConfigOptionsProvider ConfigOptions, (Compilation Compilation, ImmutableArray<MethodDeclarationSyntax?> Methods) CompilationAndMethods)> compilationAndMethods =
              context.AnalyzerConfigOptionsProvider.Combine(context.CompilationProvider.Combine(methodDeclarations.Collect()));

            context.RegisterSourceOutput(compilationAndMethods, (spc, source)
                => Execute(source.ConfigOptions.GlobalOptions, source.CompilationAndMethods.Compilation, source.CompilationAndMethods.Methods!, spc));
        }

        private void Execute(AnalyzerConfigOptions analyzerConfigOptions, Compilation compilation, ImmutableArray<MethodDeclarationSyntax> contextMethods, SourceProductionContext sourceProductionContext)
        {
            bool enableOperationInvokerGenerator =
                analyzerConfigOptions.TryGetValue("build_property.EnableCoreWCFOperationInvokerGenerator",
                    out string? enableSourceGenerator) && enableSourceGenerator == "true";

            if (!enableOperationInvokerGenerator)
            {
                return;
            }

            if (contextMethods.IsDefaultOrEmpty)
            {
                return;
            }

            OperationInvokerSourceGenerationContext context = new(sourceProductionContext);
            Parser parser = new(compilation, context);
            SourceGenerationSpec spec = parser.GetGenerationSpec(contextMethods);
            if (spec != SourceGenerationSpec.None)
            {
                Emitter emitter = new(context, spec);
                emitter.Emit();
            }
        }

        internal readonly struct OperationInvokerSourceGenerationContext
        {
            private readonly SourceProductionContext _context;

            public OperationInvokerSourceGenerationContext(SourceProductionContext context)
            {
                _context = context;
            }

            public void ReportDiagnostic(Diagnostic diagnostic)
            {
                _context.ReportDiagnostic(diagnostic);
            }

            public void AddSource(string hintName, SourceText sourceText)
            {
                _context.AddSource(hintName, sourceText);
            }
        }
    }
}
