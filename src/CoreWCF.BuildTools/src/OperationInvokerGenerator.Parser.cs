// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreWCF.BuildTools;

public sealed partial class OperationInvokerGenerator
{
    private sealed class Parser
    {
        private static readonly SymbolDisplayFormat s_methodDisplayFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeContainingType,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private readonly Compilation _compilation;
        private readonly OperationInvokerSourceGenerationContext _context;
        private readonly INamedTypeSymbol? _sSMOperationContractSymbol;
        private readonly INamedTypeSymbol? _coreWCFOperationContractSymbol;
        private readonly INamedTypeSymbol? _sSMServiceContractSymbol;
        private readonly INamedTypeSymbol? _coreWCFServiceContractSymbol;

        public Parser(Compilation compilation, in OperationInvokerSourceGenerationContext context)
        {
                _compilation = compilation;
                _context = context;

                _sSMOperationContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
                _coreWCFOperationContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");
                _sSMServiceContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
                _coreWCFServiceContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");
            }

        public SourceGenerationSpec GetGenerationSpec(ImmutableArray<InterfaceDeclarationSyntax> interfaceDeclarationSyntaxes)
        {
                var builder = ImmutableArray.CreateBuilder<OperationContractSpec>();
                HashSet<string> emittedOperations = new(StringComparer.Ordinal);

                var serviceContracts = (from interfaceDeclarationSyntax in interfaceDeclarationSyntaxes
                    let semanticModel = _compilation.GetSemanticModel(interfaceDeclarationSyntax.SyntaxTree)
                    let symbol = semanticModel.GetDeclaredSymbol(interfaceDeclarationSyntax)
                    where symbol is not null
                    let serviceContract = symbol
                    where serviceContract.GetOneAttributeOf(_sSMServiceContractSymbol, _coreWCFServiceContractSymbol) is not null
                    where !HasOpenGenericContext(serviceContract)
                    where !serviceContract.IsPrivate()
                    select serviceContract).ToImmutableArray();

                foreach (INamedTypeSymbol serviceContract in serviceContracts)
                {
                    foreach (IMethodSymbol operationContract in GetOperationContracts(serviceContract))
                    {
                        string operationKey = operationContract.ToDisplayString(s_methodDisplayFormat);
                        if (emittedOperations.Add(operationKey))
                        {
                            builder.Add(new OperationContractSpec(operationContract));
                        }
                    }
                }

                ImmutableArray<OperationContractSpec> operationContractSpecs = builder.ToImmutable();

                if (operationContractSpecs.IsEmpty)
                {
                    return SourceGenerationSpec.None;
                }

                return new SourceGenerationSpec(operationContractSpecs);
            }

        private IEnumerable<IMethodSymbol> GetOperationContracts(INamedTypeSymbol serviceContract)
        {
            foreach (INamedTypeSymbol inheritedServiceContract in EnumerateServiceContracts(serviceContract))
            {
                foreach (IMethodSymbol method in inheritedServiceContract.GetMembers().OfType<IMethodSymbol>())
                {
                    if (method.GetOneAttributeOf(_sSMOperationContractSymbol, _coreWCFOperationContractSymbol) is null)
                    {
                        continue;
                    }

                    IMethodSymbol operationContract = serviceContract.FindImplementationForInterfaceMember(method) as IMethodSymbol ?? method;
                    if (HasOpenGenericContext(operationContract) || operationContract.IsPrivate())
                    {
                        continue;
                    }

                    yield return operationContract;
                }
            }
        }

        private IEnumerable<INamedTypeSymbol> EnumerateServiceContracts(INamedTypeSymbol serviceContract)
        {
            yield return serviceContract;

            foreach (INamedTypeSymbol inheritedInterface in serviceContract.AllInterfaces)
            {
                if (inheritedInterface.GetOneAttributeOf(_sSMServiceContractSymbol, _coreWCFServiceContractSymbol) is not null)
                {
                    yield return inheritedInterface;
                }
            }
        }

        private static bool HasOpenGenericContext(IMethodSymbol method)
        {
            if (method.TypeParameters.Length > 0)
            {
                return true;
            }

            return HasOpenGenericContext(method.ContainingType);
        }

        private static bool HasOpenGenericContext(INamedTypeSymbol? containingType)
        {
            for (; containingType != null; containingType = containingType.ContainingType)
            {
                if (IsOpenGenericType(containingType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOpenGenericType(INamedTypeSymbol type)
        {
            return type.IsGenericType && type.TypeArguments.Any(ContainsTypeParameter);
        }

        private static bool ContainsTypeParameter(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.TypeParameter)
            {
                return true;
            }

            if (type is IArrayTypeSymbol arrayType)
            {
                return ContainsTypeParameter(arrayType.ElementType);
            }

            if (type is IPointerTypeSymbol pointerType)
            {
                return ContainsTypeParameter(pointerType.PointedAtType);
            }

            return type is INamedTypeSymbol namedType
                   && namedType.IsGenericType
                   && namedType.TypeArguments.Any(ContainsTypeParameter);
        }

        internal static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is InterfaceDeclarationSyntax interfaceDeclarationSyntax
                                                                             && interfaceDeclarationSyntax.AttributeLists.Count > 0;

        internal static InterfaceDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
                var interfaceDeclarationSyntax = (InterfaceDeclarationSyntax)context.Node;
                foreach (var attributeList in interfaceDeclarationSyntax.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var attributeSymbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
                        if (attributeSymbol == null)
                        {
                            continue;
                        }

                        var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                        var fullName = attributeContainingTypeSymbol.ToDisplayString();

                        if (fullName == "CoreWCF.ServiceContractAttribute" || fullName == "System.ServiceModel.ServiceContractAttribute")
                        {
                            return interfaceDeclarationSyntax;
                        }
                    }
                }

                return null;
            }
    }
}