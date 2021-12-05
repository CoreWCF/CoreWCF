// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
            private readonly OperationParameterInjectionSourceGenerationContext _sourceGenerationContext;
            private readonly Lazy<IEnumerable<SyntaxNode>> _allNodes;
            private readonly Lazy<IEnumerable<(INamedTypeSymbol ServiceImplementation, INamedTypeSymbol ServiceContract)>> _serviceImplementationsAndContracts;
            private readonly IDictionary<INamedTypeSymbol, ImmutableArray<IMethodSymbol>> _operationContracts;
            private readonly INamedTypeSymbol? _sSMOperationContractSymbol;
            private readonly INamedTypeSymbol? _coreWCFOperationContractSymbol;

            public Parser(Compilation compilation, in OperationParameterInjectionSourceGenerationContext sourceGenerationContext)
            {
                _compilation = compilation;
                _sourceGenerationContext = sourceGenerationContext;
                _allNodes = new Lazy<IEnumerable<SyntaxNode>>(() => _compilation.SyntaxTrees
                    .SelectMany(static x => x.GetRoot().DescendantNodes())
                    .ToImmutableArray());
                _serviceImplementationsAndContracts = new Lazy<IEnumerable<(INamedTypeSymbol ServiceImplementation, INamedTypeSymbol ServiceContract)>>(() => FindServiceImplementationAndContracts(_allNodes.Value));
                _operationContracts = new Dictionary<INamedTypeSymbol, ImmutableArray<IMethodSymbol>>(SymbolEqualityComparer.Default);
                _sSMOperationContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
                _coreWCFOperationContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");
            }

            private OperationContractSpec? GetOperationContractSpec(MethodDeclarationSyntax methodDeclarationSyntax)
            {
                SemanticModel model = _compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree);
                IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(methodDeclarationSyntax);
                if (methodSymbol == null)
                {
                    return null;
                }

                if (!methodSymbol.ContainingType.IsPartial())
                {
                    _sourceGenerationContext.ReportDiagnostic(DiagnosticDescriptors.ParentClassShouldBePartialError(methodSymbol.ContainingType.Name, methodSymbol.Name));
                    return null;
                }

                var allServiceContractCandidates = methodSymbol.ContainingType.AllInterfaces;
                if (allServiceContractCandidates.Length == 0)
                {
                    _sourceGenerationContext.ReportDiagnostic(DiagnosticDescriptors.ParentClassShouldImplementAServiceContractError(methodSymbol.ContainingType.Name, methodSymbol.Name));
                    return null;
                }

                bool atLeastOneServiceContractIsFound = false;

                foreach (var serviceContractCandidate in allServiceContractCandidates)
                {
                    foreach (var serviceImplementationAndContract in _serviceImplementationsAndContracts.Value)
                    {
                        if (SymbolEqualityComparer.Default.Equals(serviceImplementationAndContract.ServiceContract, serviceContractCandidate))
                        {
                            atLeastOneServiceContractIsFound = true;

                            if (!_operationContracts.ContainsKey(serviceImplementationAndContract.ServiceContract))
                            {
                                _operationContracts.Add(serviceImplementationAndContract.ServiceContract, serviceImplementationAndContract.ServiceContract.GetMembers().OfType<IMethodSymbol>()
                                    .Where(x => x.HasOneOfAttributes(_sSMOperationContractSymbol, _coreWCFOperationContractSymbol)).ToImmutableArray());
                            }

                            var operationContractCandidates = _operationContracts[serviceImplementationAndContract.ServiceContract]
                                .Where(x => x.Name == methodSymbol.Name)
                                .Where(x => x.Parameters.All(occp => methodSymbol.Parameters.Any(msp => msp.IsMatchingParameter(occp))))
                                .ToImmutableArray();

                            if (operationContractCandidates.Length == 1)
                            {
                                IMethodSymbol operationContractCandidate = operationContractCandidates[0];
                                if (serviceImplementationAndContract.ServiceImplementation.FindImplementationForInterfaceMember(operationContractCandidate) != null)
                                {
                                    _sourceGenerationContext.ReportDiagnostic(DiagnosticDescriptors.OperationContractShouldNotBeAlreadyImplementedError(operationContractCandidate.ContainingType.Name, operationContractCandidate.Name));
                                    return null;
                                }

                                return new OperationContractSpec(serviceImplementationAndContract.ServiceContract, serviceImplementationAndContract.ServiceImplementation, operationContractCandidate, methodSymbol);
                            }
                        }
                    }
                }

                if (!atLeastOneServiceContractIsFound)
                {
                    _sourceGenerationContext.ReportDiagnostic(DiagnosticDescriptors.ParentClassShouldImplementAServiceContractError(methodSymbol.ContainingType.Name, methodSymbol.Name));
                }

                return null;
            }

            public SourceGenerationSpec? GetGenerationSpec(IEnumerable<MethodDeclarationSyntax> methodDeclarationSyntaxList)
            {
                List<OperationContractSpec>? operationContractSpecs = null;

                foreach (MethodDeclarationSyntax methodDeclarationSyntax in methodDeclarationSyntaxList)
                {
                    OperationContractSpec? operationContractSpec = GetOperationContractSpec(methodDeclarationSyntax);
                    if (operationContractSpec != null)
                    {
                        (operationContractSpecs ??= new List<OperationContractSpec>()).Add(operationContractSpec);
                    }
                }

                if (operationContractSpecs == null)
                {
                    return null;
                }

                return new SourceGenerationSpec(operationContractSpecs)
                {
                    SSMOperationContractSymbol = _sSMOperationContractSymbol,
                    CoreWCFOperationContractSymbol = _coreWCFOperationContractSymbol,
                    TaskSymbol = _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                    GenericTaskSymbol = _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                    CoreWCFInjectedSymbol = _compilation.GetTypeByMetadataName("CoreWCF.InjectedAttribute")
                };
            }

            private IEnumerable<(INamedTypeSymbol service, INamedTypeSymbol contract)> FindServiceImplementationAndContracts(
                IEnumerable<SyntaxNode> allNodes)
            {
                var serviceContracts = FindServiceContracts(allNodes);

                IEnumerable<ClassDeclarationSyntax> allClasses = allNodes
                    .Where(static d => d.IsKind(SyntaxKind.ClassDeclaration))
                    .OfType<ClassDeclarationSyntax>();

                foreach (var @class in allClasses)
                {
                    var model = _compilation.GetSemanticModel(@class.SyntaxTree);
                    var namedTypeSymbol = model.GetDeclaredSymbol(@class);

                    if (namedTypeSymbol == null)
                    {
                        continue;
                    }

                    foreach (var @interface in namedTypeSymbol.AllInterfaces)
                    {
                        foreach (var serviceContract in serviceContracts)
                        {
                            if (SymbolEqualityComparer.Default.Equals(serviceContract, @interface))
                            {
                                yield return (namedTypeSymbol, serviceContract);
                            }
                        }
                    }
                }
            }

            private IEnumerable<INamedTypeSymbol> FindServiceContracts(IEnumerable<SyntaxNode> allNodes)
            {
                var SSMServiceContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
                var CoreWCFServiceContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");

                IEnumerable<InterfaceDeclarationSyntax> allInterfaces = allNodes
                   .Where(static d => d.IsKind(SyntaxKind.InterfaceDeclaration))
                   .OfType<InterfaceDeclarationSyntax>();

                foreach (var @interface in allInterfaces)
                {
                    var model = _compilation.GetSemanticModel(@interface.SyntaxTree);
                    var symbol = model.GetDeclaredSymbol(@interface);
                    if (symbol == null)
                    {
                        continue;
                    }

                    if (symbol.HasOneOfAttributes(SSMServiceContractSymbol, CoreWCFServiceContractSymbol))
                    {
                        yield return symbol;
                    }
                }

                var referenceServiceContracts = new List<INamedTypeSymbol>();

                foreach (var reference in _compilation.References)
                {
                    var assemblySymbol = _compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                    if (assemblySymbol == null)
                    {
                        continue;
                    }

                    var visitor = new FindAllServiceContractsVisitor(referenceServiceContracts, new INamedTypeSymbol?[]
                    {
                        SSMServiceContractSymbol,
                        CoreWCFServiceContractSymbol
                    });

                    visitor.Visit(assemblySymbol.GlobalNamespace);
                }

                foreach (var serviceContract in referenceServiceContracts)
                {
                    yield return serviceContract;
                }
            }

            internal static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is MethodDeclarationSyntax methodDeclarationSyntax &&
                methodDeclarationSyntax.ParameterList.Parameters.Count > 0
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

                            if (fullName == "CoreWCF.InjectedAttribute")
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
