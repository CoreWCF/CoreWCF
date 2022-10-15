// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CoreWCF.BuildTools;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AuthorizationAttributesAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
        new[] { DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupported, DiagnosticDescriptors.AuthorizeAttributeIsNotSupportedOnClass }
        .ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(WarnAboutAllowAnonymousUsages, SymbolKind.Method, SymbolKind.NamedType);
        context.RegisterSymbolAction(BreakBuildWhenAuthorizeOnServiceContractImplementation, SymbolKind.NamedType);
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    private static void BreakBuildWhenAuthorizeOnServiceContractImplementation(SymbolAnalysisContext context)
    {
        var authorizeAttribute = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.AuthorizeAttribute");
        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");

        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        bool hasAuthorizeAttribute = namedTypeSymbol.HasOneOfAttributes(authorizeAttribute);

        if (!hasAuthorizeAttribute)
        {
            return;
        }

        if (namedTypeSymbol.AllInterfaces.Any(x =>
                x.HasOneOfAttributes(ssmServiceContractAttribute, coreWCFServiceContractAttribute)))
        {
            context.ReportDiagnostic(
                DiagnosticDescriptors.AuthorizeAttributeIsNotSupportedOnClassWarning(namedTypeSymbol.Name, context.Symbol.Locations[0]));
        }
    }

    private static void WarnAboutAllowAnonymousUsages(SymbolAnalysisContext context)
    {
        var allowAnonymousAttribute = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute");

        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");

        var ssmOperationContractAttribute = context.Compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
        var coreWCFOperationContractAttribute = context.Compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");

        if (context.Symbol is IMethodSymbol methodSymbol)
        {
            bool hasAllowAnonymous = methodSymbol.HasOneOfAttributes(allowAnonymousAttribute);
            if (!hasAllowAnonymous)
            {
                return;
            }

            var operationContracts = from @interface in methodSymbol.ContainingType.AllInterfaces
                where @interface.HasOneOfAttributes(ssmServiceContractAttribute, coreWCFServiceContractAttribute)
                from methods in @interface.GetMembers().OfType<IMethodSymbol>()
                where methods.HasOneOfAttributes(coreWCFOperationContractAttribute, ssmOperationContractAttribute)
                select methods;

            bool isOperationContractImplementation = operationContracts.Any(operationContract =>
                SymbolEqualityComparer.Default.Equals(
                    methodSymbol.ContainingType.FindImplementationForInterfaceMember(operationContract), methodSymbol));

            if (isOperationContractImplementation)
            {
                context.ReportDiagnostic(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupportedWarning(context.Symbol.Locations[0]));
            }

            return;
        }

        if (context.Symbol is INamedTypeSymbol namedTypeSymbol)
        {
            bool hasAllowAnonymous = namedTypeSymbol.HasOneOfAttributes(allowAnonymousAttribute);
            if (!hasAllowAnonymous)
            {
                return;
            }

            if (namedTypeSymbol.AllInterfaces.Any(x => x.HasOneOfAttributes(coreWCFServiceContractAttribute, ssmServiceContractAttribute)))
            {
                context.ReportDiagnostic(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupportedWarning(namedTypeSymbol.Locations[0]));
            }
        }
    }
}
