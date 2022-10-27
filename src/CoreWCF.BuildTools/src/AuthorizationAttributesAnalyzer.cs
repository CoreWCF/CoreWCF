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
    private const string AuthenticationSchemesPropertyName = "AuthenticationSchemes";
    private const string RolesPropertyName = "Roles";
    private const string AuthorizeDataTypeName = "Microsoft.AspNetCore.Authorization.IAuthorizeData";
    private const string AllowAnonymousTypeName = "Microsoft.AspNetCore.Authorization.IAllowAnonymous";
    private const string CoreWCFServiceContractAttributeTypeName = "CoreWCF.ServiceContractAttribute";
    private const string CoreWCFOperationContractAttributeTypeName = "CoreWCF.OperationContractAttribute";
    private const string SSMServiceContractAttributeTypeName = "System.ServiceModel.ServiceContractAttribute";
    private const string SSMOperationContractAttributeTypeName = "System.ServiceModel.OperationContractAttribute";
    private const string CoreWCFInjectedAttributeTypeName = "CoreWCF.InjectedAttribute";
    private const string MVCFromServicesAttributeTypeName = "Microsoft.AspNetCore.Mvc.FromServicesAttribute";

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
        new[]
            {
                DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AllowAnonymousAttributeIsNotSupported,
                DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AuthorizeAttributeIsNotSupportedOnClass,
                DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AuthorizeDataAuthenticationSchemesPropertyIsNotSupported,
                DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AuthorizeDataRolesPropertyIsNotSupported
            }
            .ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
#if !DEBUG
        context.EnableConcurrentExecution();
#endif
        context.RegisterSymbolAction(WarnWhenAllowAnonymousOnServiceContractImplementation, SymbolKind.NamedType);
        context.RegisterSymbolAction(WarnWhenAllowAnonymousOnOperationContractImplementation, SymbolKind.Method);
        context.RegisterSymbolAction(WarnWhenAuthorizeDataWithAuthenticationSchemesOrRolesPropertySetOnOperationContractImplementation, SymbolKind.Method);
        context.RegisterSymbolAction(ErrorWhenAuthorizeOnServiceContractImplementation, SymbolKind.NamedType);
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    private static void ErrorWhenAuthorizeOnServiceContractImplementation(SymbolAnalysisContext context)
    {
        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        var authorizeData = context.Compilation.GetTypeByMetadataName(AuthorizeDataTypeName);
        bool hasAuthorizeData = namedTypeSymbol.HasOneAttributeInheritFrom(authorizeData);
        if (!hasAuthorizeData)
        {
            return;
        }

        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName(SSMServiceContractAttributeTypeName);
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFServiceContractAttributeTypeName);

        if (namedTypeSymbol.AllInterfaces.Any(x => x.GetOneAttributeOf(ssmServiceContractAttribute, coreWCFServiceContractAttribute) is not null))
        {
            context.ReportDiagnostic(
                DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AuthorizeAttributeIsNotSupportedOnClassWarning(namedTypeSymbol.Name, context.Symbol.Locations[0]));
        }
    }

    private static void WarnWhenAuthorizeDataWithAuthenticationSchemesOrRolesPropertySetOnOperationContractImplementation(SymbolAnalysisContext context)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.IsGeneratedCode() == true)
        {
            return;
        }

        var authorizeData = context.Compilation.GetTypeByMetadataName(AuthorizeDataTypeName);
        bool hasAuthorizeData = methodSymbol.HasOneAttributeInheritFrom(authorizeData);
        if (!hasAuthorizeData)
        {
            return;
        }

        bool isOperationContractImplementation = IsOperationContractImplementation(context, methodSymbol);
        if (!isOperationContractImplementation)
        {
            return;
        }

        var arguments = from attribute in methodSymbol.GetAttributes()
            where attribute.AttributeClass!.AllInterfaces.Any(@interface =>
                @interface.Equals(authorizeData, SymbolEqualityComparer.Default))
            from args in attribute.NamedArguments
            select args;

        foreach (var argument in arguments)
        {
            if (argument.Key == AuthenticationSchemesPropertyName)
            {
                context.ReportDiagnostic(DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AuthorizeDataAuthenticationSchemesPropertyIsNotSupportedWarning(context.Symbol.Locations[0]));
                continue;
            }
            if (argument.Key == RolesPropertyName)
            {
                context.ReportDiagnostic(DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AuthorizeDataRolesPropertyIsNotSupportedWarning(context.Symbol.Locations[0]));
            }
        }
    }

    private static void WarnWhenAllowAnonymousOnOperationContractImplementation(SymbolAnalysisContext context)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.IsGeneratedCode() == true)
        {
            return;
        }

        var allowAnonymous = context.Compilation.GetTypeByMetadataName(AllowAnonymousTypeName);
        bool hasAllowAnonymous = methodSymbol.HasOneAttributeInheritFrom(allowAnonymous);
        if (!hasAllowAnonymous)
        {
            return;
        }

        bool isOperationContractImplementation = IsOperationContractImplementation(context, methodSymbol);

        if (isOperationContractImplementation)
        {
            context.ReportDiagnostic(DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AllowAnonymousAttributeIsNotSupportedWarning(context.Symbol.Locations[0]));
        }
    }

    private static bool IsOperationContractImplementation(SymbolAnalysisContext context, IMethodSymbol methodSymbol)
    {
        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName(SSMServiceContractAttributeTypeName);
        var coreWCFServiceContractAttribute =
            context.Compilation.GetTypeByMetadataName(CoreWCFServiceContractAttributeTypeName);

        var serviceContracts = (from @interface in methodSymbol.ContainingType.AllInterfaces
            where @interface.GetOneAttributeOf(ssmServiceContractAttribute, coreWCFServiceContractAttribute) is not null
            select @interface).ToImmutableArray();

        if (serviceContracts.IsEmpty)
        {
            return false;
        }

        var ssmOperationContractAttribute = context.Compilation.GetTypeByMetadataName(SSMOperationContractAttributeTypeName);
        var coreWCFOperationContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFOperationContractAttributeTypeName);

        var operationContracts = (from serviceContract in serviceContracts
            from method in serviceContract.GetMembers().OfType<IMethodSymbol>()
            where method.Name == methodSymbol.Name
            where method.GetOneAttributeOf(coreWCFOperationContractAttribute, ssmOperationContractAttribute) is not null
            select method).ToImmutableArray();

        var implementedMethods = methodSymbol.ContainingType.GetMembers().OfType<IMethodSymbol>()
            .Where(x => x.Name == methodSymbol.Name)
            .ToImmutableArray();

        var coreWCFInjectedAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFInjectedAttributeTypeName);
        var mvcFromServicesAttribute = context.Compilation.GetTypeByMetadataName(MVCFromServicesAttributeTypeName);

        foreach (IMethodSymbol operationContract in operationContracts)
        {
            if (implementedMethods.Any(implementedMethod =>
                    implementedMethod.IsMatchingUserProvidedMethod(operationContract, coreWCFInjectedAttribute,
                        mvcFromServicesAttribute)))
            {
                return true;
            }
        }

        return false;
    }

    private static void WarnWhenAllowAnonymousOnServiceContractImplementation(SymbolAnalysisContext context)
    {
        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        var allowAnonymous = context.Compilation.GetTypeByMetadataName(AllowAnonymousTypeName);
        bool hasAllowAnonymous = namedTypeSymbol.HasOneAttributeInheritFrom(allowAnonymous);
        if (!hasAllowAnonymous)
        {
            return;
        }

        var ssmServiceContractAttribute = context.Compilation.GetTypeByMetadataName(SSMServiceContractAttributeTypeName);
        var coreWCFServiceContractAttribute = context.Compilation.GetTypeByMetadataName(CoreWCFServiceContractAttributeTypeName);

        if (namedTypeSymbol.AllInterfaces.Any(x =>
                x.GetOneAttributeOf(coreWCFServiceContractAttribute, ssmServiceContractAttribute) is not null))
        {
            context.ReportDiagnostic(DiagnosticDescriptors.AuthorizationAttributesAnalyzer_02XX.AllowAnonymousAttributeIsNotSupportedWarning(namedTypeSymbol.Locations[0]));
        }
    }
}
