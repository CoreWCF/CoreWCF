// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Microsoft.CodeAnalysis.Text;

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
            private IDictionary<INamedTypeSymbol, ImmutableArray<IMethodSymbol>> _operationContracts;

            public Parser(Compilation compilation, in OperationParameterInjectionSourceGenerationContext sourceGenerationContext)
            {
                _compilation = compilation;
                _sourceGenerationContext = sourceGenerationContext;
                _allNodes = new Lazy<IEnumerable<SyntaxNode>>(() => _compilation.SyntaxTrees
                    .SelectMany(x => x.GetRoot().DescendantNodes())
                    .ToImmutableArray());
                _serviceImplementationsAndContracts = new Lazy<IEnumerable<(INamedTypeSymbol ServiceImplementation, INamedTypeSymbol ServiceContract)>>(() => FindServiceImplementationAndContracts(_allNodes.Value));
                _operationContracts = new Dictionary<INamedTypeSymbol, ImmutableArray<IMethodSymbol>>(SymbolEqualityComparer.Default);
            }

            public SourceGenerationSpec? GetGenerationSpec(IEnumerable<MethodDeclarationSyntax> methodDeclarationSyntaxList)
            {
                List<OperationContractSpec>? operationContractSpecs = null;
                foreach (MethodDeclarationSyntax methodDeclarationSyntax in methodDeclarationSyntaxList)
                {
                    if (!methodDeclarationSyntax.HasParentPartialClass())
                    {
                        _sourceGenerationContext.ReportDiagnostic(DiagnosticDescriptors.ServicesShouldBePartialError("", ""));
                        continue;
                    }

                    SemanticModel model = _compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree);
                    IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(methodDeclarationSyntax);

                    var allServiceContractCandidates = methodSymbol.ContainingType.AllInterfaces;
                    if(allServiceContractCandidates.Length == 0)
                    {
                        // raise error ParentClass does not implement any interface
                    }

                    foreach (var serviceContractCandidate in allServiceContractCandidates)
                    {
                        bool atLeastOneServiceContractIsFound = false;
                        foreach (var serviceImplementationAndContract in _serviceImplementationsAndContracts.Value)
                        {
                            if(SymbolEqualityComparer.Default.Equals(serviceImplementationAndContract.ServiceContract, serviceContractCandidate))
                            {
                                atLeastOneServiceContractIsFound = true;

                                if (!_operationContracts.ContainsKey(serviceImplementationAndContract.ServiceContract))
                                {
                                    var SSMOperationContractSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
                                    var CoreWCFOperationContractSymbol = _compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");

                                    _operationContracts.Add(serviceImplementationAndContract.ServiceContract, serviceImplementationAndContract.ServiceContract.GetMembers().OfType<IMethodSymbol>()
                                        .Where(x => x.HasOneOfAttributes(SSMOperationContractSymbol, CoreWCFOperationContractSymbol)).ToImmutableArray());
                                }
                                
                                var operationContractCandidates = _operationContracts[serviceImplementationAndContract.ServiceContract]
                                    .Where(x => x.Name == methodSymbol.Name)
                                    .Where(x => x.Parameters.All(occp => methodSymbol.Parameters.Any(msp => msp.IsMatchingParameter(occp))))
                                    .ToImmutableArray();

                                if(operationContractCandidates.Length == 1)
                                {
                                    IMethodSymbol operationContractCandidate = operationContractCandidates[0];
                                    if (serviceImplementationAndContract.ServiceImplementation.FindImplementationForInterfaceMember(operationContractCandidate) != null)
                                    {
                                        // raise error because there is already an implementation 
                                    }

                                    (operationContractSpecs ??= new List<OperationContractSpec>()).Add(new OperationContractSpec
                                    {
                                        ServiceContract = serviceImplementationAndContract.ServiceContract,
                                        ServiceContractImplementation = serviceImplementationAndContract.ServiceImplementation,
                                        OperationContract = operationContractCandidates.Single(),
                                        OperationContractImplementationCandidate = methodSymbol
                                    });
                                }
                            }
                        }

                        if (!atLeastOneServiceContractIsFound)
                        {
                            // raise error because ParentClass does not implement any ServiceContract
                        }
                    }
                }

                if(operationContractSpecs == null)
                {
                    return null;
                }

                if (operationContractSpecs.Count > 0)
                {
                    return new SourceGenerationSpec
                    {
                        SSMServiceBehaviorSymbol = _compilation.GetTypeByMetadataName("System.ServiceModel.ServiceBehaviorAttribute"),
                        CoreWCFServiceBehaviorSymbol = _compilation.GetTypeByMetadataName("CoreWCF.ServiceBehaviorAttribute"),
                        TaskSymbol = _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                        GenericTaskSymbol = _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),

                        OperationContractSpecs = operationContractSpecs
                    };
                }

                return null;
            }

            private IEnumerable<(INamedTypeSymbol service, INamedTypeSymbol contract)> FindServiceImplementationAndContracts(
                IEnumerable<SyntaxNode> allNodes)
            {
                var serviceContracts = FindServiceContracts(allNodes);

                IEnumerable<ClassDeclarationSyntax> allClasses = allNodes
                    .Where(d => d.IsKind(SyntaxKind.ClassDeclaration))
                    .OfType<ClassDeclarationSyntax>();

                foreach (var @class in allClasses)
                {
                    var model = _compilation.GetSemanticModel(@class.SyntaxTree);
                    var typeSymbol = model.GetDeclaredSymbol(@class);

                    foreach (var @interface in typeSymbol.AllInterfaces)
                    {
                        foreach (var serviceContract in serviceContracts)
                        {
                            if (SymbolEqualityComparer.Default.Equals(serviceContract, @interface))
                            {
                                yield return (typeSymbol, serviceContract);
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
                   .Where(d => d.IsKind(SyntaxKind.InterfaceDeclaration))
                   .OfType<InterfaceDeclarationSyntax>();

                foreach (var @interface in allInterfaces)
                {
                    var model = _compilation.GetSemanticModel(@interface.SyntaxTree);
                    var symbol = model.GetDeclaredSymbol(@interface);
                    if (symbol.HasOneOfAttributes(SSMServiceContractSymbol, CoreWCFServiceContractSymbol))
                    {
                        yield return symbol;
                    }
                }

                var referenceServiceContracts = new List<INamedTypeSymbol>();

                foreach (var reference in _compilation.References.Reverse())
                {
                    var assemblySymbol = _compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
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
