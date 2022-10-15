// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreWCF.BuildTools
{
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
            private readonly INamedTypeSymbol? _coreWCFInjectedSymbol;
            private readonly INamedTypeSymbol? _mvcFromServicesSymbol;

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
                _coreWCFInjectedSymbol = _compilation.GetTypeByMetadataName("CoreWCF.InjectedAttribute");
                _mvcFromServicesSymbol = _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromServicesAttribute");
            }

            public SourceGenerationSpec GetGenerationSpec(ImmutableArray<MethodDeclarationSyntax> methodDeclarationSyntaxes)
            {
                ImmutableArray<IMethodSymbol> methods = (from methodDeclarationSyntax in methodDeclarationSyntaxes
                    let semanticModel = _compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree)
                    let symbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax)
                    where symbol is not null
                    let methodSymbol = symbol as IMethodSymbol
                    select methodSymbol).ToImmutableArray();

                var methodServiceContractAndOperationContractsValues = from method in methods
                    from @interface in method.ContainingType.AllInterfaces
                    where @interface.HasOneOfAttributes(_sSMServiceContractSymbol, _coreWCFServiceContractSymbol)
                    let methodMembers = (from member in @interface.GetMembers()
                        let methodMember = member as IMethodSymbol
                        where methodMember is not null
                        where methodMember.HasOneOfAttributes(_sSMOperationContractSymbol, _coreWCFOperationContractSymbol)
                        select methodMember).ToImmutableArray()
                    select (Method: method, ServiceContract: @interface, OperationContracts: methodMembers);

                var methodMissingOperationServiceContractAndOperationContractsValues =
                    from value in methodServiceContractAndOperationContractsValues
                    let missingOperationContract =
                        value.OperationContracts
                            .SingleOrDefault(x => x.IsMatchingUserProvidedMethod(value.Method, _coreWCFInjectedSymbol, _mvcFromServicesSymbol))
                    where missingOperationContract is not null
                    let nonNullMissingOperationContract = missingOperationContract as IMethodSymbol
                    select (value.Method, MissingOperationContract: nonNullMissingOperationContract,
                        value.ServiceContract, value.OperationContracts);

                var builder = ImmutableArray.CreateBuilder<OperationContractSpec>();

                foreach (var value in methodMissingOperationServiceContractAndOperationContractsValues)
                {
                    if (!value.Method.ContainingType.IsPartial(out INamedTypeSymbol parentType))
                    {
                        _context.ReportDiagnostic(DiagnosticDescriptors.RaiseParentClassShouldBePartialError(parentType.Name, value.Method.Name, parentType.Locations[0]));
                        continue;
                    }

                    builder.Add(new OperationContractSpec(value.ServiceContract,
                        value.Method.ContainingType, value.MissingOperationContract,
                        value.Method,
                        _httpContextSymbol, _httpRequestSymbol, _httpResponseSymbol));
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
                    _compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromServicesAttribute"));
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

                            if (fullName == "Microsoft.AspNetCore.Mvc.FromServicesAttribute" || fullName == "CoreWCF.InjectedAttribute")
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
}
