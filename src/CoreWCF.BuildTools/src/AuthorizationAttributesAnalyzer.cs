// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CoreWCF.BuildTools;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AuthorizationAttributesAnalyzer : DiagnosticAnalyzer
{
    private const string AuthorizeAttributeName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";
    private const string AllowAnonymousAttributeName = "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute";
    private const string CoreWCFServiceContractAttributeName = "CoreWCF.ServiceContractAttribute";
    private const string CoreWCFOperationContractAttributeName = "CoreWCF.OperationContractAttribute";
    private const string SSMServiceContractAttributeName = "System.ServiceModel.ServiceContractAttribute";
    private const string SSMOperationContractAttributeName = "System.ServiceModel.OperationContractAttribute";
    private const string CoreWCFInjectedAttributeName = "CoreWCF.InjectedAttribute";
    private const string MVCFromServicesAttributeName = "Microsoft.AspNetCore.Mvc.FromServicesAttribute";

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
        new[] { DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupported, DiagnosticDescriptors.AuthorizeAttributeIsNotSupportedOnClass }
        .ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(WarnWhenAllowAnonymousOnServiceContractImplementation, SymbolKind.NamedType);
        context.RegisterSymbolAction(WarnWhenAllowAnonymousOnOperationContractImplementation, SymbolKind.Method);
        context.RegisterSymbolAction(ErrorWhenAuthorizeOnServiceContractImplementation, SymbolKind.NamedType);
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    private static void ErrorWhenAuthorizeOnServiceContractImplementation(SymbolAnalysisContext context)
    {
        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        var authorizeAttribute = context.Compilation.GetTypeByMetadataName(AuthorizeAttributeName);
        bool hasAuthorizeAttribute = namedTypeSymbol.HasOneOfAttributes(authorizeAttribute);
        if (!hasAuthorizeAttribute)
        {
            return;
        }

        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName(SSMServiceContractAttributeName);
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFServiceContractAttributeName);

        if (namedTypeSymbol.AllInterfaces.Any(x =>
                x.HasOneOfAttributes(ssmServiceContractAttribute, coreWCFServiceContractAttribute)))
        {
            context.ReportDiagnostic(
                DiagnosticDescriptors.AuthorizeAttributeIsNotSupportedOnClassWarning(namedTypeSymbol.Name, context.Symbol.Locations[0]));
        }
    }

    private static void WarnWhenAllowAnonymousOnOperationContractImplementation(SymbolAnalysisContext context)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        var allowAnonymousAttribute = context.Compilation.GetTypeByMetadataName(AllowAnonymousAttributeName);
        bool hasAllowAnonymous = methodSymbol.HasOneOfAttributes(allowAnonymousAttribute);
        if (!hasAllowAnonymous)
        {
            return;
        }

        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName(SSMServiceContractAttributeName);
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFServiceContractAttributeName);

        var serviceContracts = (from @interface in methodSymbol.ContainingType.AllInterfaces
            where @interface.HasOneOfAttributes(ssmServiceContractAttribute, coreWCFServiceContractAttribute)
            select @interface).ToImmutableArray();

        if (serviceContracts.IsEmpty)
        {
            return;
        }

        var ssmOperationContractAttribute = context.Compilation.GetTypeByMetadataName(SSMOperationContractAttributeName);
        var coreWCFOperationContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFOperationContractAttributeName);

        var operationContracts = (from serviceContract in serviceContracts
            from method in serviceContract.GetMembers().OfType<IMethodSymbol>()
            where method.Name == methodSymbol.Name
            where method.HasOneOfAttributes(coreWCFOperationContractAttribute, ssmOperationContractAttribute)
            select method).ToImmutableArray();

        var implementedMethods = methodSymbol.ContainingType.GetMembers().OfType<IMethodSymbol>()
            .Where(x => x.Name == methodSymbol.Name)
            .ToImmutableArray();

        var coreWCFInjectedAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFInjectedAttributeName);
        var mvcFromServicesAttribute = context.Compilation.GetTypeByMetadataName(MVCFromServicesAttributeName);

        bool isOperationContractImplementation = false;
        foreach (IMethodSymbol operationContract in operationContracts)
        {
            if (implementedMethods.Any(implementedMethod => implementedMethod.IsMatchingUserProvidedMethod(operationContract, coreWCFInjectedAttribute, mvcFromServicesAttribute)))
            {
                isOperationContractImplementation = true;
                break;
            }
        }

        if (isOperationContractImplementation)
        {
            context.ReportDiagnostic(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupportedWarning(context.Symbol.Locations[0]));
        }
    }

    private static void WarnWhenAllowAnonymousOnServiceContractImplementation(SymbolAnalysisContext context)
    {
        var allowAnonymousAttribute = context.Compilation.GetTypeByMetadataName(AllowAnonymousAttributeName);

        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        bool hasAllowAnonymous = namedTypeSymbol.HasOneOfAttributes(allowAnonymousAttribute);
        if (!hasAllowAnonymous)
        {
            return;
        }

        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName(SSMServiceContractAttributeName);
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFServiceContractAttributeName);

        if (namedTypeSymbol.AllInterfaces.Any(x =>
                x.HasOneOfAttributes(coreWCFServiceContractAttribute, ssmServiceContractAttribute)))
        {
            context.ReportDiagnostic(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupportedWarning(namedTypeSymbol.Locations[0]));
        }
    }
}
