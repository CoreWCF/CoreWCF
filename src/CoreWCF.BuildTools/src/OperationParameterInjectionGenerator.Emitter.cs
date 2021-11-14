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
            private class Indentor
            {
                const string ____ = "    ";
                const string ________ = "        ";
                const string ____________ = "            ";
                const string ________________ = "                ";
                const string ____________________ = "                    ";

                public int Level { get; private set; } = 0;
                public void Increment()
                {
                    Level++;
                }

                public void Decrement()
                {
                    Level--;
                }

                public override string ToString() => Level switch
                {
                    0 => string.Empty,
                    1 => ____,
                    2 => ________,
                    3 => ____________,
                    4 => ________________,
                    5 => ____________________,
                    _ => throw new InvalidOperationException(),
                };
            }

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
                string fileName = $"{operationContractSpec.ServiceContract.ContainingNamespace.ToDisplayString().Replace(".", "_")}_{operationContractSpec.ServiceContract.Name}_{operationContractSpec.MissingOperationContract.Name}.g.cs";
                var dependencies = operationContractSpec.UserProvidedOperationContractImplementation.Parameters.Where(x => !operationContractSpec.MissingOperationContract.Parameters.Any(p =>
                       p.IsMatchingParameter(x))).ToArray();

                bool shouldGenerateAsyncAwait = SymbolEqualityComparer.Default.Equals(operationContractSpec.MissingOperationContract.ReturnType, _generationSpec.TaskSymbol)
                    || (operationContractSpec.MissingOperationContract.ReturnType is INamedTypeSymbol symbol &&
                    SymbolEqualityComparer.Default.Equals(symbol.ConstructedFrom, _generationSpec.GenericTaskSymbol));

                Dictionary<ITypeSymbol, string> dependencyNames = new(SymbolEqualityComparer.Default);

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

                string returnType = operationContractSpec.MissingOperationContract.ReturnsVoid
                    ? "void"
                    : $"{operationContractSpec.MissingOperationContract.ReturnType}";

                string parameters = string.Join(", ", operationContractSpec.MissingOperationContract.Parameters
                    .Select(p => p.RefKind switch
                    {
                        RefKind.Ref => $"ref {p.Type} {p.Name}",
                        RefKind.Out => $"out {p.Type} {p.Name}",
                        _ => $"{p.Type} {p.Name}",
                    }));

                var indentor = new Indentor();
                var builder = new StringBuilder();

                builder.AppendLine($@"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace {operationContractSpec.ServiceContractImplementation.ContainingNamespace}
{{");
                indentor.Increment();
                builder.AppendLine($@"{indentor}{accessibilityModifier}partial class {operationContractSpec.ServiceContractImplementation.Name}");
                builder.AppendLine($@"{indentor}{{");
                indentor.Increment();
                builder.AppendLine($@"{indentor}public {@async}{returnType} {operationContractSpec.MissingOperationContract.Name}({parameters})");
                builder.AppendLine($@"{indentor}{{");
                indentor.Increment();
                builder.AppendLine($@"{indentor}var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();");
                builder.AppendLine($@"{indentor}if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");");
                builder.AppendLine($@"{indentor}if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)");
                builder.AppendLine($@"{indentor}{{");
                indentor.Increment();
                builder.AppendLine($@"{indentor}using (var scope = serviceProvider.CreateScope())");
                builder.AppendLine($@"{indentor}{{");
                indentor.Increment();

                string dependencyNamePrefix = "d";
                AppendResolveDependencies("scope.ServiceProvider");
                AppendInvokeUserProvidedImplementation();

                if (operationContractSpec.MissingOperationContract.ReturnsVoid || SymbolEqualityComparer.Default.Equals(operationContractSpec.MissingOperationContract.ReturnType, _generationSpec.TaskSymbol))
                {
                    builder.AppendLine($@"{indentor}return;");
                }

                indentor.Decrement();
                builder.AppendLine($@"{indentor}}}");
                indentor.Decrement();
                builder.AppendLine($@"{indentor}}}");

                dependencyNamePrefix = "e";
                AppendResolveDependencies("serviceProvider");
                AppendInvokeUserProvidedImplementation();

                indentor.Decrement();
                builder.AppendLine($@"{indentor}}}");
                indentor.Decrement();
                builder.AppendLine($@"{indentor}}}");
                indentor.Decrement();
                builder.AppendLine($@"{indentor}}}");

                _sourceGenerationContext.AddSource(fileName, SourceText.From(builder.ToString(), Encoding.UTF8, SourceHashAlgorithm.Sha256));

                void AppendResolveDependencies(string serviceProviderName)
                {
                    for (int i = 0; i < dependencies.Length; i++)
                    {
                        dependencyNames[dependencies[i].Type] = $"{dependencyNamePrefix}{i}";
                        builder.AppendLine($@"{indentor}var {dependencyNamePrefix}{i} = {serviceProviderName}.GetService<{dependencies[i].Type}>();");
                    }
                }

                void AppendInvokeUserProvidedImplementation()
                {
                    builder.Append($"{indentor}{@return}{@await}{operationContractSpec.UserProvidedOperationContractImplementation.Name}(");
                    for (int i = 0; i < operationContractSpec.UserProvidedOperationContractImplementation.Parameters.Length; i++)
                    {
                        IParameterSymbol parameter = operationContractSpec.UserProvidedOperationContractImplementation.Parameters[i];
                        if (i != 0)
                        {
                            builder.Append(", ");
                        }

                        if (parameter.HasOneOfAttributes(_generationSpec.CoreWCFInjectedSymbol))
                        {
                            builder.Append(dependencyNames[parameter.Type]);
                        }
                        else
                        {
                            builder.Append(parameter.RefKind switch
                            {
                                RefKind.Ref => $"ref {parameter.Name}",
                                RefKind.Out => $"out {parameter.Name}",
                                _ => parameter.Name,
                            });
                        }
                    }
                    builder.AppendLine(");");
                }
            }
        }
    }
}
