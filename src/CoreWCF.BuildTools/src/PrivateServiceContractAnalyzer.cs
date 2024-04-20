// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CoreWCF.BuildTools;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateServiceContractAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
        new[]
            {
                DiagnosticDescriptors.PrivateServiceContractAnalyzer__03XX.PrivateServiceContract,
            }
            .ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
#if !DEBUG
        context.EnableConcurrentExecution();
#endif
        context.RegisterSyntaxNodeAction(ErrorWhenServiceContractIsPrivateInterface, SyntaxKind.InvocationExpression);
    }

    private void ErrorWhenServiceContractIsPrivateInterface(SyntaxNodeAnalysisContext context)
    {
        bool enableOperationInvokerGenerator = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.EnableCoreWCFOperationInvokerGenerator", out string? enableSourceGenerator) && enableSourceGenerator == "true";

        if (!enableOperationInvokerGenerator)
        {
            return;
        }

        InvocationExpressionSyntax invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;
        IMethodSymbol? methodSymbol = context.SemanticModel.GetSymbolInfo(invocationExpressionSyntax, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol == null)
        {
            return;
        }

        INamedTypeSymbol? coreWcfServiceContractSymbol = context.Compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");
        INamedTypeSymbol? sSMServiceContractSymbol =
            context.Compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
        INamedTypeSymbol? coreWcfOperationContractSymbol = context.Compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");
        INamedTypeSymbol? sSMOperationContractSymbol =
            context.Compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");

        IEnumerable<INamedTypeSymbol> serviceContracts;
        if (methodSymbol.ContainingType?.TypeKind == TypeKind.Interface)
        {
            serviceContracts = new[] { methodSymbol.ContainingType };
        }
        else
        {
            serviceContracts = from @interface in methodSymbol.ContainingType.AllInterfaces
                where @interface.GetOneAttributeOf(coreWcfServiceContractSymbol, sSMServiceContractSymbol) is not null
                select @interface;
        }

        var privateServiceContracts = from @interface in serviceContracts
            where @interface.IsPrivate()
            select @interface;

        var operationContracts = from @interface in privateServiceContracts
            from member in @interface.GetMembers()
            let methodMember = member as IMethodSymbol
            where methodMember is not null
            let operationContractAttribute = methodMember.GetOneAttributeOf(coreWcfOperationContractSymbol, sSMOperationContractSymbol)
            where operationContractAttribute is not null
            select (@interface, methodMember);

        foreach (var (privateServiceContract, operationContract) in operationContracts)
        {
            context.ReportDiagnostic(DiagnosticDescriptors.PrivateServiceContractAnalyzer__03XX.PrivateServiceContractError(privateServiceContract.ToDisplayString(), context.Node.GetLocation()));
        }
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;
}
