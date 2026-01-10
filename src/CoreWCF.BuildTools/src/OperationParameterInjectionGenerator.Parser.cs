// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreWCF.BuildTools;

public sealed partial class OperationParameterInjectionGenerator
{
    private sealed class Parser
    {
        private readonly Compilation _compilation;
        private readonly OperationParameterInjectionSourceGenerationContext _context;
        private readonly INamedTypeSymbol? _sSMOperationContractSymbol;
        private readonly INamedTypeSymbol? _coreWCFOperationContractSymbol;
        private readonly INamedTypeSymbol? _httpContextSymbol;
        private readonly INamedTypeSymbol? _httpRequestSymbol;
        private readonly INamedTypeSymbol? _httpResponseSymbol;
        private readonly INamedTypeSymbol? _sSMServiceContractSymbol;
        private readonly INamedTypeSymbol? _coreWCFServiceContractSymbol;

        public Parser(Compilation compilation, in OperationParameterInjectionSourceGenerationContext context)
        {
                _compilation = compilation;
                _context = context;

                _sSMOperationContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
                _coreWCFOperationContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");
                _sSMServiceContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
                _coreWCFServiceContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");
                _httpContextSymbol = _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.HttpContext");
                _httpRequestSymbol = _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.HttpRequest");
                _httpResponseSymbol = _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.HttpResponse");
            }

        public SourceGenerationSpec GetGenerationSpec(ImmutableArray<MethodDeclarationSyntax> methodDeclarationSyntaxes)
        {
                ImmutableArray<IMethodSymbol> methods = (from methodDeclarationSyntax in methodDeclarationSyntaxes
                    let semanticModel = _compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree)
                    let symbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax)
                    where symbol is not null
                    let methodSymbol = symbol as IMethodSymbol
                    select methodSymbol).ToImmutableArray();

                var coreWCFInjectedSymbol = _compilation.GetTypeByMetadataName("CoreWCF.InjectedAttribute");
                var fromServicesSymbol = _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromServicesAttribute");
                var fromKeyedServicesSymbol = _compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute");

                // Query for interface-based service contracts
                var interfaceBasedContracts = from method in methods
                    from @interface in method.ContainingType.AllInterfaces
                    where @interface.GetOneAttributeOf(_sSMServiceContractSymbol, _coreWCFServiceContractSymbol) is not null
                    let methodMembers = (from member in @interface.GetMembers()
                        let methodMember = member as IMethodSymbol
                        where methodMember is not null
                        let operationContractAttribute = methodMember.GetOneAttributeOf(_sSMOperationContractSymbol,
                            _coreWCFOperationContractSymbol)
                        where operationContractAttribute is not null
                        select (MethodMember: methodMember, AttributeData: operationContractAttribute)).ToImmutableArray()
                    select (Method: method, ServiceContract: (INamedTypeSymbol)@interface, OperationContracts: methodMembers, IsClassBased: false);

                // Query for class-based service contracts
                // For class-based contracts, we need methods that have:
                // 1. OperationContract attribute
                // 2. Containing type has ServiceContract attribute
                // Note: We don't filter for injected parameters here - the matching logic will handle it
                var classBasedContracts = from method in methods
                    let containingType = method.ContainingType
                    where containingType.GetOneAttributeOf(_sSMServiceContractSymbol, _coreWCFServiceContractSymbol) is not null
                    let operationContractAttribute = method.GetOneAttributeOf(_sSMOperationContractSymbol, _coreWCFOperationContractSymbol)
                    where operationContractAttribute is not null
                    // For class-based contracts, the method itself serves as the operation contract
                    let methodMembers = ImmutableArray.Create((MethodMember: method, AttributeData: operationContractAttribute))
                    select (Method: method, ServiceContract: containingType, OperationContracts: methodMembers, IsClassBased: true);

                // Combine both interface-based and class-based contracts
                var methodServiceContractAndOperationContractsValues = interfaceBasedContracts.Concat(classBasedContracts);

                var methodMissingOperationServiceContractAndOperationContractsValues =
                    from value in methodServiceContractAndOperationContractsValues
                    let missingOperationContract =
                        value.OperationContracts
                            .SingleOrDefault(x => x.MethodMember.Name == value.Method.Name
                                                  && (value.IsClassBased || // For class-based, always match the method itself
                                                      x.MethodMember.Parameters.All(p =>
                                                          value.Method.Parameters.Any(msp =>
                                                              msp.IsMatchingParameter(p)))))
                    where missingOperationContract.MethodMember is not null
                    let nonNullMissingOperationContract = missingOperationContract
                    select (value.Method, MissingOperationContract: nonNullMissingOperationContract,
                        value.ServiceContract, value.OperationContracts);

                var builder = ImmutableArray.CreateBuilder<OperationContractSpec>();

                foreach (var value in methodMissingOperationServiceContractAndOperationContractsValues)
                {
                    if (!value.Method.ContainingType.IsPartial(out INamedTypeSymbol parentType))
                    {
                        _context.ReportDiagnostic(DiagnosticDescriptors.OperationParameterInjectionGenerator_01XX.RaiseParentClassShouldBePartialError(parentType.Name, value.Method.Name, parentType.Locations[0]));
                        continue;
                    }

                    builder.Add(new OperationContractSpec(value.ServiceContract,
                        value.Method.ContainingType, value.MissingOperationContract.MethodMember,
                        value.Method,
                        _httpContextSymbol, _httpRequestSymbol, _httpResponseSymbol, value.MissingOperationContract.AttributeData));
                }

                ImmutableArray<OperationContractSpec> operationContractSpecs = builder.ToImmutable();

                if (operationContractSpecs.IsEmpty)
                {
                    return SourceGenerationSpec.None;
                }

                return new SourceGenerationSpec(operationContractSpecs,
                    _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                    _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                    _compilation.GetTypeByMetadataName("CoreWCF.InjectedAttribute"),
                    _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromServicesAttribute"),
                    _compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute")
                );
            }

        internal static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is MethodDeclarationSyntax methodDeclarationSyntax
                 && methodDeclarationSyntax.ParameterList.Parameters.Count > 0
                 && methodDeclarationSyntax.ParameterList.Parameters.Any(static p => p.AttributeLists.Count > 0)
                 && (methodDeclarationSyntax.Body != null || methodDeclarationSyntax.ExpressionBody != null);

        internal static MethodDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
                var methodDeclarationSyntax = (MethodDeclarationSyntax)context.Node;
                foreach (var parameterSyntax in methodDeclarationSyntax.ParameterList.Parameters)
                {
                    foreach (var attributeList in parameterSyntax.AttributeLists)
                    {
                        foreach (var attributeSyntax in attributeList.Attributes)
                        {
                            IMethodSymbol? attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
                            if (attributeSymbol == null)
                            {
                                continue;
                            }

                            INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                            string fullName = attributeContainingTypeSymbol.ToDisplayString();

                            if (fullName is "Microsoft.AspNetCore.Mvc.FromServicesAttribute"
                                or "CoreWCF.InjectedAttribute"
                                or "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute")
                            {
                                return methodDeclarationSyntax;
                            }
                        }
                    }
                }

                return null;
            }
    }
}
