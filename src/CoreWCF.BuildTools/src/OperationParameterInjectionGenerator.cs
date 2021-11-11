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
    [Generator]
    public class OperationParameterInjectionGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            IEnumerable<SyntaxNode> allNodes = context.Compilation.SyntaxTrees
                .SelectMany(x => x.GetRoot().DescendantNodes())
                .ToImmutableArray();

            var serviceContracts = FindServiceContracts(context, allNodes).ToImmutableArray();

            foreach (var serviceImplAndContract in FindServiceImplAndContracts(context, allNodes, serviceContracts).ToImmutableArray())
            {
                var operationContracts = FindOperationContracts(context, serviceImplAndContract.contract).ToImmutableArray();

                bool isErrorDiagnosticIssued = false;

                foreach (var operationContract in operationContracts)
                {
                    var operationContractImplementation = serviceImplAndContract.service.FindImplementationForInterfaceMember(operationContract);

                    if(operationContractImplementation == null)
                    {
                        var implementationCandidates = FindOperationContractImplementationCandidate(context, serviceImplAndContract.service, operationContract).ToImmutableArray();
                        if(implementationCandidates.Length == 1)
                        {
                            if (serviceImplAndContract.isPartialClass)
                            {
                                GenerateOperationContractImplementation(context, serviceImplAndContract.service, serviceImplAndContract.contract, implementationCandidates[0], operationContract);
                            }
                            else if (!isErrorDiagnosticIssued)
                            {
                                context.ReportDiagnostic(DiagnosticDescriptors.ServicesShouldBePartialError(serviceImplAndContract.service.Name, serviceImplAndContract.contract.Name));
                                isErrorDiagnosticIssued = true;
                            }
                        }
                        // TODO multiple candidates..
                    }
                    // TODO service is already implemented. should we warn when we detect a candidate
                }
            }
        }

        private static bool GetInstanceContextMode(GeneratorExecutionContext context, INamedTypeSymbol service)
        {
            var CoreWCFServiceBehaviorSymbol = context.Compilation.GetTypeByMetadataName("CoreWCF.ServiceBehaviorAttribute");
            var SSMServiceBehaviorSymbol = context.Compilation.GetTypeByMetadataName("System.ServiceModel.ServiceBehaviorAttribute");
            

            var serviceBehaviorAttribute = service.GetAttributes()
                .Where(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, CoreWCFServiceBehaviorSymbol)
                    || SymbolEqualityComparer.Default.Equals(x.AttributeClass, SSMServiceBehaviorSymbol))
                .FirstOrDefault();

            if(serviceBehaviorAttribute == null)
            {
                return false;
            }

            foreach (var argument in serviceBehaviorAttribute.NamedArguments)
            {
                if (argument.Key == "InstanceContextMode" && !argument.Value.IsNull)
                {
                    return argument.Value.Value.ToString() == "2";
                }
            }

            return false;
        }

        private static void GenerateOperationContractImplementation(GeneratorExecutionContext context, INamedTypeSymbol service, INamedTypeSymbol contract, IMethodSymbol operationContractImplementation, IMethodSymbol operationContract)
        {
            var SSMServiceBehaviorSymbol = context.Compilation.GetTypeByMetadataName("System.ServiceModel.ServiceBehaviorAttribute");
            var CoreWCFServiceBehaviorSymbol = context.Compilation.GetTypeByMetadataName("CoreWCF.ServiceBehaviorAttribute");
            var TaskSymbol = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var GenericTaskSymbol = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

            string fileName = $"{contract.ContainingNamespace.ToDisplayString().Replace(".", "_")}_{contract.Name}_{operationContract.Name}.cs";
            var dependencies = operationContractImplementation.Parameters.Where(x => !operationContract.Parameters.Any(p =>
                   p.IsMatchingParameter(x))).ToArray();

            bool isInstanceContextModeSingle = GetInstanceContextMode(context, service);

            bool shouldGenerateAsyncAwait = SymbolEqualityComparer.Default.Equals(operationContract.ReturnType, TaskSymbol)
                ||(operationContract.ReturnType is INamedTypeSymbol symbol && 
                SymbolEqualityComparer.Default.Equals(symbol.ConstructedFrom, GenericTaskSymbol));

            string @async = shouldGenerateAsyncAwait
                ? "async "
                : string.Empty;

            string @await = shouldGenerateAsyncAwait
                ? "await "
                : string.Empty;

            string @return = (operationContract.ReturnsVoid || SymbolEqualityComparer.Default.Equals(operationContract.ReturnType, TaskSymbol)) ?
                string.Empty
                : "return ";

            string accessibilityModifier = service.DeclaredAccessibility switch
            {
                Accessibility.Public => "public ",
                _ => "internal "
            };

            const string OneTab = "    ";
            const string ThreeTabs = OneTab + OneTab + OneTab;

            var builder = new StringBuilder();

            builder.Append($@"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace {service.ContainingNamespace}
{{
    {accessibilityModifier}partial class {service.Name}
    {{
");
            builder.Append($@"        public {@async}{GetReturnType()} {operationContract.Name}({GetParameters()})
        {{
");
            builder.Append($@"            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
");
            AddDependenciesResolution("scope.ServiceProvider", "                    ", "d");
            AddMethodCall("                    ", "d");

            if (operationContract.ReturnsVoid || SymbolEqualityComparer.Default.Equals(operationContract.ReturnType, TaskSymbol))
            {
                builder.Append($@"
                    return;");
            }

            builder.Append($@"
                }}
            }}
");
            AddDependenciesResolution("serviceProvider", "            ", "e");
            AddMethodCall("            ", "e");
            builder.Append($@"
        }}
    }}
}}
");
            context.AddSource(fileName, SourceText.From(builder.ToString(), Encoding.UTF8, SourceHashAlgorithm.Sha256));

            string GetReturnType()
                => operationContract.ReturnsVoid ?
                    "void"
                    : $"{operationContract.ReturnType}";

            string GetParameters()
                => string.Join(", ", operationContract.Parameters
                    .Select(p => $"{p.Type} {p.Name}"));
            
            void AddDependenciesResolution(string serviceProviderName, string prefix, string dependencyPrefix)
            {              
                for (int i = 0; i < dependencies.Length; i++)
                {
                    builder.AppendLine($@"{prefix}var {dependencyPrefix}{i} = {serviceProviderName}.GetService<{dependencies[i].Type}>();");
                }
            }

            void AddMethodCall(string prefix, string dependencyPrefix)
            {
                var dependenciesParameters = Enumerable.Range(0, dependencies.Length).Select(x => $"{dependencyPrefix}{x}");
     
                builder.Append($"{prefix}{@return}{@await}{operationContract.Name}({string.Join(", ", operationContract.Parameters.Select(x => x.Name).Union(dependenciesParameters))});");
            }
        }

        private static IEnumerable<IMethodSymbol> FindOperationContractImplementationCandidate(GeneratorExecutionContext context, INamedTypeSymbol service, IMethodSymbol operationContract)
        {
            var CoreWCFInjectedAttribute = context.Compilation.GetTypeByMetadataName("CoreWCF.InjectedAttribute");
            // Find a candidate implementation with same name as contract
            // and with all non injected parameters
            var implementationCandidates =
                from serviceMethod in service.GetMembers().OfType<IMethodSymbol>()
                where serviceMethod.Name == operationContract.Name
                where serviceMethod.Parameters.Any(x => x.HasAttribute(CoreWCFInjectedAttribute))
                where operationContract.Parameters.All(ocp => serviceMethod.Parameters.Any(smp => smp.IsMatchingParameter(ocp)))
                select serviceMethod;
            return implementationCandidates;
        }

        private static IEnumerable<IMethodSymbol> FindOperationContracts(GeneratorExecutionContext context, ITypeSymbol serviceContract)
        {
            var SSMOperationContractSymbol = context.Compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
            var CoreWCFOperationContractSymbol = context.Compilation.GetTypeByMetadataName("CoreWCF.OperationContractAttribute");

            return serviceContract.GetMembers().OfType<IMethodSymbol>()
                .Where(x => x.HasAttribute(SSMOperationContractSymbol) || x.HasAttribute(CoreWCFOperationContractSymbol));
        }

        private static IEnumerable<(INamedTypeSymbol service, INamedTypeSymbol contract, bool isPartialClass)> FindServiceImplAndContracts(GeneratorExecutionContext context,
            IEnumerable<SyntaxNode> allNodes,
            IEnumerable<INamedTypeSymbol> serviceContracts)
        {
            IEnumerable<ClassDeclarationSyntax> allClasses = allNodes
                .Where(d => d.IsKind(SyntaxKind.ClassDeclaration))
                .OfType<ClassDeclarationSyntax>();
            foreach (var @class in allClasses)
            {
                var model = context.Compilation.GetSemanticModel(@class.SyntaxTree);
                var typeSymbol = model.GetDeclaredSymbol(@class);
                
                foreach (var @interface in typeSymbol.AllInterfaces)
                {
                    foreach (var serviceContract in serviceContracts)
                    {
                        if (SymbolEqualityComparer.Default.Equals(serviceContract, @interface))
                        {
                            bool isPartialClass = @class.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword));
                            yield return (typeSymbol, serviceContract, isPartialClass);
                        }
                    }
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> FindServiceContracts(GeneratorExecutionContext context, IEnumerable<SyntaxNode> allNodes)
        {
            var SSMServiceContractSymbol = context.Compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
            var CoreWCFServiceContractSymbol = context.Compilation.GetTypeByMetadataName("CoreWCF.ServiceContractAttribute");

            IEnumerable<InterfaceDeclarationSyntax> allInterfaces = allNodes
               .Where(d => d.IsKind(SyntaxKind.InterfaceDeclaration))
               .OfType<InterfaceDeclarationSyntax>();

            foreach (var @interface in allInterfaces)
            {
                var model = context.Compilation.GetSemanticModel(@interface.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(@interface);
                if(symbol.HasAttribute(SSMServiceContractSymbol) || symbol.HasAttribute(CoreWCFServiceContractSymbol))
                {
                    yield return symbol;
                }
            }

            var referenceServiceContracts = new List<INamedTypeSymbol>();

            foreach (var reference in context.Compilation.References.Reverse())
            {
                var assemblySymbol = context.Compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                var abc = new FindAllServiceContractsVisitor(referenceServiceContracts, new[]
                {
                    SSMServiceContractSymbol,
                    CoreWCFServiceContractSymbol
                });

                abc.Visit(assemblySymbol.GlobalNamespace);
            }

            foreach (var serviceContract in referenceServiceContracts)
            {
                yield return serviceContract;
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {

        }


        class FindAllServiceContractsVisitor : SymbolVisitor
        {
            private readonly IList<INamedTypeSymbol> _symbols;
            private readonly IEnumerable<INamedTypeSymbol> _serviceContractSymbols;

            public FindAllServiceContractsVisitor(IList<INamedTypeSymbol> symbols, IEnumerable<INamedTypeSymbol> serviceContractSymbols)
            {
                _symbols = symbols;
                _serviceContractSymbols = serviceContractSymbols;
            }

            public override void VisitNamespace(INamespaceSymbol symbol)
            {
                foreach (var child in symbol.GetMembers())
                {
                    child.Accept(this);
                }
            }

            public override void VisitNamedType(INamedTypeSymbol symbol)
            {
                if(symbol.TypeKind == TypeKind.Interface)
                {
                    if(_serviceContractSymbols.Any(x => symbol.HasAttribute(x)))
                    {
                        _symbols.Add(symbol);
                    }
                }
            }
        }
    }
}
