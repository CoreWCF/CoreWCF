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

        IncrementalValuesProvider<InterfaceDeclarationSyntax> coreWCFInterfaces = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "CoreWCF.ServiceContractAttribute",
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        IncrementalValuesProvider<InterfaceDeclarationSyntax> ssmInterfaces = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "System.ServiceModel.ServiceContractAttribute",
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        IncrementalValueProvider<ImmutableArray<InterfaceDeclarationSyntax>> interfaceDeclarations = coreWCFInterfaces.Collect()
            .Combine(ssmInterfaces.Collect())
            .Select(static (pair, _) => pair.Left.AddRange(pair.Right));

        IncrementalValueProvider<(bool Enabled, (Compilation Compilation, ImmutableArray<InterfaceDeclarationSyntax> Interfaces) CompilationAndMethods)> compilationAndMethods =
            enabledProvider.Combine(context.CompilationProvider.Combine(interfaceDeclarations));

        context.RegisterSourceOutput(compilationAndMethods, (spc, source)
            => Execute(source.Enabled, source.CompilationAndMethods.Compilation, source.CompilationAndMethods.Interfaces, spc));
    }

    private void Execute(bool enabled, Compilation compilation, ImmutableArray<InterfaceDeclarationSyntax> contextInterfaces, SourceProductionContext sourceProductionContext)
    {
        if (!enabled)
        {
            return;
        }

        if (contextInterfaces.IsDefaultOrEmpty)
        {
            return;
        }

        OperationInvokerSourceGenerationContext context = new(sourceProductionContext);
        Parser parser = new(compilation, context);
        SourceGenerationSpec spec = parser.GetGenerationSpec(contextInterfaces);
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