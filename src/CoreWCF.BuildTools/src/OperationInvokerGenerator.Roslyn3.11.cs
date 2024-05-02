// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools
{
    [Generator(LanguageNames.CSharp)]
    public sealed partial class OperationInvokerGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(static () => new SyntaxContextReceiver());
        }

        public void Execute(GeneratorExecutionContext executionContext)
        {
            bool enableOperationInvokerGenerator = executionContext.AnalyzerConfigOptions.GlobalOptions
                .TryGetValue("build_property.EnableCoreWCFOperationInvokerGenerator", out string? enableSourceGenerator)
                && enableSourceGenerator == "true";

            if (!enableOperationInvokerGenerator)
            {
                return;
            }

            if (executionContext.SyntaxContextReceiver is not SyntaxContextReceiver receiver || receiver.MethodDeclarationSyntaxList == null)
            {
                // nothing to do yet
                return;
            }

            OperationInvokerSourceGenerationContext context = new(executionContext);
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

        internal readonly struct OperationInvokerSourceGenerationContext
        {
            private readonly GeneratorExecutionContext _context;

            public OperationInvokerSourceGenerationContext(GeneratorExecutionContext context)
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
