// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools;

[Generator(LanguageNames.CSharp)]
public sealed partial class OperationInvokerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<bool> enabledProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                options.GlobalOptions.TryGetValue("build_property.EnableCoreWCFOperationInvokerGenerator", out string? val)
                && val == "true");

        IncrementalValuesProvider<MethodDeclarationSyntax> coreWCFMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "CoreWCF.OperationContractAttribute",
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => (MethodDeclarationSyntax)ctx.TargetNode);

        IncrementalValuesProvider<MethodDeclarationSyntax> ssmMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "System.ServiceModel.OperationContractAttribute",
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => (MethodDeclarationSyntax)ctx.TargetNode);

        IncrementalValueProvider<ImmutableArray<MethodDeclarationSyntax>> methodDeclarations = coreWCFMethods.Collect()
            .Combine(ssmMethods.Collect())
            .Select(static (pair, _) => pair.Left.AddRange(pair.Right));

        IncrementalValueProvider<(bool Enabled, (Compilation Compilation, ImmutableArray<MethodDeclarationSyntax> Methods) CompilationAndMethods)> compilationAndMethods =
            enabledProvider.Combine(context.CompilationProvider.Combine(methodDeclarations));

        context.RegisterSourceOutput(compilationAndMethods, (spc, source)
            => Execute(source.Enabled, source.CompilationAndMethods.Compilation, source.CompilationAndMethods.Methods, spc));
    }

    private void Execute(bool enabled, Compilation compilation, ImmutableArray<MethodDeclarationSyntax> contextMethods, SourceProductionContext sourceProductionContext)
    {
        if (!enabled)
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