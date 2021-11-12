using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools
{
    [Generator]
    public sealed partial class OperationParameterInjectionGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider.CreateSyntaxProvider<MethodDeclarationSyntax>(
                predicate: (token, _) => Parser.IsSyntaxTargetForGeneration(token),
                transform: static (s, _) => Parser.GetSemanticTargetForGeneration(s))
                .Where(static c => c is not null);

            IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
              context.CompilationProvider.Combine(methodDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndMethods, (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> contextMethods, SourceProductionContext sourceProductionContext)
        {
            if (contextMethods.IsDefaultOrEmpty)
            {
                return;
            }

            OperationParameterInjectionSourceGenerationContext context = new OperationParameterInjectionSourceGenerationContext(sourceProductionContext);
            Parser parser = new(compilation, context);
            SourceGenerationSpec? spec = parser.GetGenerationSpec(contextMethods);
            if (spec != null)
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
}
