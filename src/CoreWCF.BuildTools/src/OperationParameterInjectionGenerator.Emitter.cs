// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools
{
    public sealed partial class OperationParameterInjectionGenerator
    {
        private sealed class Emitter
        {
            private readonly OperationParameterInjectionSourceGenerationContext _sourceGenerationContext;
            private SourceGenerationSpec _generationSpec;

            public Emitter(in OperationParameterInjectionSourceGenerationContext sourceGenerationContext, SourceGenerationSpec generationSpec)
            {
                _sourceGenerationContext = sourceGenerationContext;
                _generationSpec = generationSpec;
            }


            public void Emit()
            {
                foreach (var operationContractSpec in _generationSpec.OperationContractSpecs)
                {
                    EmitOperationContract(operationContractSpec);
                }
            }

            private void EmitOperationContract(OperationContractSpec operationContractSpec)
            {
                string fileName = $"{operationContractSpec.ServiceContract.ContainingNamespace.ToDisplayString().Replace(".", "_")}_{operationContractSpec.ServiceContract.Name}_{operationContractSpec.MissingOperationContract.Name}.cs";
                var dependencies = operationContractSpec.UserProvidedOperationContractImplementation.Parameters.Where(x => !operationContractSpec.MissingOperationContract.Parameters.Any(p =>
                       p.IsMatchingParameter(x))).ToArray();

                bool shouldGenerateAsyncAwait = SymbolEqualityComparer.Default.Equals(operationContractSpec.MissingOperationContract.ReturnType, _generationSpec.TaskSymbol)
                    || (operationContractSpec.MissingOperationContract.ReturnType is INamedTypeSymbol symbol &&
                    SymbolEqualityComparer.Default.Equals(symbol.ConstructedFrom, _generationSpec.GenericTaskSymbol));

                Dictionary<ITypeSymbol, string> dependencyNames = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);

                string @async = shouldGenerateAsyncAwait
                    ? "async "
                    : string.Empty;

                string @await = shouldGenerateAsyncAwait
                    ? "await "
                    : string.Empty;

                string @return = (operationContractSpec.MissingOperationContract.ReturnsVoid || SymbolEqualityComparer.Default.Equals(operationContractSpec.MissingOperationContract.ReturnType, _generationSpec.TaskSymbol)) ?
                    string.Empty
                    : "return ";

                string accessibilityModifier = operationContractSpec.ServiceContractImplementation.DeclaredAccessibility switch
                {
                    Accessibility.Public => "public ",
                    _ => "internal "
                };

                var builder = new StringBuilder();

                builder.Append($@"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace {operationContractSpec.ServiceContractImplementation.ContainingNamespace}
{{
    {accessibilityModifier}partial class {operationContractSpec.ServiceContractImplementation.Name}
    {{
");
                builder.Append($@"        public {@async}{GetReturnType()} {operationContractSpec.MissingOperationContract.Name}({GetParameters()})
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

                if (operationContractSpec.MissingOperationContract.ReturnsVoid || SymbolEqualityComparer.Default.Equals(operationContractSpec.MissingOperationContract.ReturnType, _generationSpec.TaskSymbol))
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
                _sourceGenerationContext.AddSource(fileName, SourceText.From(builder.ToString(), Encoding.UTF8, SourceHashAlgorithm.Sha256));

                string GetReturnType()
                    => operationContractSpec.MissingOperationContract.ReturnsVoid ?
                        "void"
                        : $"{operationContractSpec.MissingOperationContract.ReturnType}";

                string GetParameters()
                    => string.Join(", ", operationContractSpec.MissingOperationContract.Parameters
                        .Select(p => $"{p.Type} {p.Name}"));

                void AddDependenciesResolution(string serviceProviderName, string prefix, string dependencyPrefix)
                {
                    for (int i = 0; i < dependencies.Length; i++)
                    {
                        dependencyNames[dependencies[i].Type] = $"{dependencyPrefix}{i}";
                        builder.AppendLine($@"{prefix}var {dependencyPrefix}{i} = {serviceProviderName}.GetService<{dependencies[i].Type}>();");
                    }
                }

                void AddMethodCall(string prefix, string dependencyPrefix)
                {
                    builder.Append($"{prefix}{@return}{@await}{operationContractSpec.UserProvidedOperationContractImplementation.Name}(");//({string.Join(", ", operationContractSpec.OperationContract.Parameters.Select(x => x.Name).Union(dependenciesParameters))});");
                    for (int i = 0; i < operationContractSpec.UserProvidedOperationContractImplementation.Parameters.Length; i++)
                    {
                        if (i != 0)
                        {
                            builder.Append(", ");
                        }

                        if (operationContractSpec.UserProvidedOperationContractImplementation.Parameters[i].HasOneOfAttributes(_generationSpec.CoreWCFInjectedSymbol))
                        {
                            builder.Append(dependencyNames[operationContractSpec.UserProvidedOperationContractImplementation.Parameters[i].Type]);
                        }
                        else
                        {
                            builder.Append(operationContractSpec.UserProvidedOperationContractImplementation.Parameters[i].Name);
                        }
                    }
                    builder.Append(");");
                }
            }
        }
    }
}
