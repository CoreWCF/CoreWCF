using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools;

[Generator(LanguageNames.CSharp)]
public sealed partial class OperationParameterInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<MethodDeclarationSyntax?> coreWCFInjected = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "CoreWCF.InjectedAttribute",
                predicate: IsInjectedParameter,
                transform: static (ctx, _) => ctx.TargetNode.Parent?.Parent as MethodDeclarationSyntax);

        IncrementalValuesProvider<MethodDeclarationSyntax?> fromServices = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Microsoft.AspNetCore.Mvc.FromServicesAttribute",
                predicate: IsInjectedParameter,
                transform: static (ctx, _) => ctx.TargetNode.Parent?.Parent as MethodDeclarationSyntax);

        IncrementalValuesProvider<MethodDeclarationSyntax?> fromKeyedServices = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute",
                predicate: IsInjectedParameter,
                transform: static (ctx, _) => ctx.TargetNode.Parent?.Parent as MethodDeclarationSyntax);

        IncrementalValueProvider<ImmutableArray<MethodDeclarationSyntax>> methodDeclarations = coreWCFInjected.Collect()
            .Combine(fromServices.Collect())
            .Combine(fromKeyedServices.Collect())
            .Select(static (pair, _) =>
            {
                var ((injected, services), keyed) = pair;
                var seen = new HashSet<(string FilePath, TextSpan Span)>();
                var builder = ImmutableArray.CreateBuilder<MethodDeclarationSyntax>();
                foreach (var method in injected)
                {
                    if (method is not null && seen.Add((method.SyntaxTree.FilePath, method.Span)))
                    {
                        builder.Add(method);
                    }
                }
                foreach (var method in services)
                {
                    if (method is not null && seen.Add((method.SyntaxTree.FilePath, method.Span)))
                    {
                        builder.Add(method);
                    }
                }
                foreach (var method in keyed)
                {
                    if (method is not null && seen.Add((method.SyntaxTree.FilePath, method.Span)))
                    {
                        builder.Add(method);
                    }
                }
                return builder.ToImmutable();
            });

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<MethodDeclarationSyntax> Methods)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations);

        context.RegisterSourceOutput(compilationAndMethods, (spc, source)
            => Execute(source.Compilation, source.Methods, spc));
    }

    private static bool IsInjectedParameter(SyntaxNode node, System.Threading.CancellationToken _) =>
        node is ParameterSyntax p
        && p.Parent?.Parent is MethodDeclarationSyntax m
        && (m.Body != null || m.ExpressionBody != null);

    private void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> contextMethods, SourceProductionContext sourceProductionContext)
    {
        if (contextMethods.IsDefaultOrEmpty)
        {
            return;
        }

        OperationParameterInjectionSourceGenerationContext context = new(sourceProductionContext);
        Parser parser = new(compilation, context);
        SourceGenerationSpec spec = parser.GetGenerationSpec(contextMethods);
        if (spec != SourceGenerationSpec.None)
        {
            Emitter emitter = new(context, spec);
            emitter.Emit();
        }
    }

    internal readonly struct OperationParameterInjectionSourceGenerationContext
    {
        private readonly SourceProductionContext _context;

        public OperationParameterInjectionSourceGenerationContext(SourceProductionContext context)
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