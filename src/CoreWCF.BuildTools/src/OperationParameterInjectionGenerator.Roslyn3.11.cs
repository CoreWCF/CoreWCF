using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools
{
    [Generator]
    public sealed partial class OperationParameterInjectionGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(static () => new SyntaxContextReceiver());
        }

        public void Execute(GeneratorExecutionContext executionContext)
        {
            if (executionContext.SyntaxContextReceiver is not SyntaxContextReceiver receiver || receiver.MethodDeclarationSyntaxList == null)
            {
                // nothing to do yet
                return;
            }

            OperationParameterInjectionSourceGenerationContext context = new(executionContext);
            Parser parser = new(executionContext.Compilation, context);
            SourceGenerationSpec spec = parser.GetGenerationSpec(receiver.MethodDeclarationSyntaxList.ToImmutableArray());
            if (spec != SourceGenerationSpec.None)
            {
                Emitter emitter = new(context, spec);
                emitter.Emit();
            }
        }

        private sealed class SyntaxContextReceiver : ISyntaxContextReceiver
        {
            public List<MethodDeclarationSyntax>? MethodDeclarationSyntaxList { get; private set; }

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (Parser.IsSyntaxTargetForGeneration(context.Node))
                {
                    MethodDeclarationSyntax? methodDeclarationSyntax = Parser.GetSemanticTargetForGeneration(context);
                    if (methodDeclarationSyntax != null)
                    {
                        (MethodDeclarationSyntaxList ??= new List<MethodDeclarationSyntax>()).Add(methodDeclarationSyntax);
                    }
                }
            }
        }

        internal readonly struct OperationParameterInjectionSourceGenerationContext
        {
            private readonly GeneratorExecutionContext _context;

            public OperationParameterInjectionSourceGenerationContext(GeneratorExecutionContext context)
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
