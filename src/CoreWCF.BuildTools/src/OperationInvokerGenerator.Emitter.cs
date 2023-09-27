// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.BuildTools
{
    public sealed partial class OperationInvokerGenerator
    {
        private sealed class Emitter
        {
            private readonly StringBuilder _builder;

            private readonly OperationInvokerSourceGenerationContext _sourceGenerationContext;
            private readonly SourceGenerationSpec _generationSpec;

            public Emitter(in OperationInvokerSourceGenerationContext sourceGenerationContext, in SourceGenerationSpec generationSpec)
            {
                _sourceGenerationContext = sourceGenerationContext;
                _generationSpec = generationSpec;
                _builder = new StringBuilder();
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
                _builder.Clear();

                var indentor = new Indentor();
                _builder.AppendLine($$"""
using System;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    file sealed class ModuleInitializerAttribute : Attribute { }
}

namespace CoreWCF.Dispatcher
{
    file sealed class OperationInvoker : CoreWCF.Dispatcher.IOperationInvoker
    {
""");
                indentor.Increment();
                indentor.Increment();

                INamedTypeSymbol? returnTypeSymbol = operationContractSpec.Method!.ReturnType as INamedTypeSymbol;
                bool isGenericTaskReturnType = returnTypeSymbol != null &&
                    returnTypeSymbol.IsGenericType &&
                    returnTypeSymbol.ConstructUnboundGenericType().ToDisplayString() == "System.Threading.Tasks.Task<>";
                bool isTaskReturnType = operationContractSpec.Method.ReturnType.ToDisplayString() == "System.Threading.Tasks.Task";
                bool isAsync = isGenericTaskReturnType || isTaskReturnType;

                string asyncString = isAsync ? "async " : string.Empty;
                _builder.AppendLine($"{indentor}public { asyncString }ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)");
                _builder.AppendLine($"{indentor}{{");
                indentor.Increment();

                int inputParameterCount = 0;
                int outputParameterCount = 0;

                List<(int, int, IParameterSymbol)> outputParams = new();
                int i = 0;
                List<string> invocationParams = new();
                foreach (var parameter in operationContractSpec.Method.Parameters)
                {
                    _builder.AppendLine($"{indentor}{parameter.Type.ToDisplayString()} p{i};");
                    if (FlowsIn(parameter))
                    {
                        _builder.AppendLine($"{indentor}p{i} = ({parameter.Type.ToDisplayString()})inputs[{inputParameterCount}];");
                        inputParameterCount++;
                    }

                    if (FlowOut(parameter))
                    {

                        outputParams.Add((outputParameterCount, i, parameter));
                        outputParameterCount++;
                    }

                    invocationParams.Add($"{GetRefKind(parameter)}p{i}");
                    i++;
                }

                if (isAsync)
                {
                    if (isTaskReturnType)
                    {
                        _builder.AppendLine($"{indentor}await (({operationContractSpec.Method.ContainingType.ToDisplayString()})instance).{operationContractSpec.Method.Name}({string.Join(", ", invocationParams)});");
                    }
                    else
                    {
                        _builder.AppendLine($"{indentor}var result = await (({operationContractSpec.Method.ContainingType.ToDisplayString()})instance).{operationContractSpec.Method.Name}({string.Join(", ", invocationParams)});");
                    }
                }
                else
                {
                    if (operationContractSpec.Method.ReturnsVoid)
                    {
                        _builder.AppendLine($"{indentor}(({operationContractSpec.Method.ContainingType.ToDisplayString()})instance).{operationContractSpec.Method.Name}({string.Join(", ", invocationParams)});");
                    }
                    else
                    {
                        _builder.AppendLine($"{indentor}var result = (({operationContractSpec.Method.ContainingType.ToDisplayString()})instance).{operationContractSpec.Method.Name}({string.Join(", ", invocationParams)});");
                    }
                }

                _builder.AppendLine($"{indentor}var outputs = AllocateOutputs();");

                foreach (var (ouputIndex, parameterIndex, parameter) in outputParams)
                {
                    _builder.AppendLine($"{indentor}outputs[{ouputIndex}] = p{parameterIndex};");
                }

                if (isAsync)
                {
                    if (isTaskReturnType)
                    {
                        _builder.AppendLine($"{indentor}return (null, outputs);");
                    }
                    else
                    {
                        _builder.AppendLine($"{indentor}return (result, outputs);");
                    }
                }
                else
                {
                    if (operationContractSpec.Method.ReturnsVoid)
                    {
                        _builder.AppendLine($"{indentor}return new ValueTask<(object, object[])>((null, outputs));");
                    }
                    else
                    {
                        _builder.AppendLine($"{indentor}return new ValueTask<(object, object[])>((result, outputs));");
                    }
                }

                indentor.Decrement();
                _builder.AppendLine($"{indentor}}}");
                _builder.AppendLine();
                _builder.Append($"{indentor}public object[] AllocateInputs() => ");
                if (inputParameterCount == 0)
                {
                    _builder.AppendLine("Array.Empty<object>();");
                }
                else
                {
                    _builder.AppendLine($"new object[{inputParameterCount}];");
                }
                _builder.AppendLine();
                _builder.Append($"{indentor}private object[] AllocateOutputs() => ");
                if (outputParameterCount == 0)
                {
                    _builder.AppendLine("Array.Empty<object>();");
                }
                else
                {
                    _builder.AppendLine($"new object[{outputParameterCount}];");
                }
                _builder.AppendLine();

                _builder.AppendLine($"{indentor}[System.Runtime.CompilerServices.ModuleInitializer]");
                _builder.Append($"{indentor}internal static void RegisterOperationInvoker() => ");
                _builder.AppendLine($"CoreWCF.Dispatcher.DispatchOperationRuntimeHelpers.RegisterOperationInvoker(\"{operationContractSpec.Method.ToDisplayString()}\", new OperationInvoker());");

                indentor.Decrement();
                _builder.AppendLine($"{indentor}}}");

                indentor.Decrement();
                _builder.AppendLine($"{indentor}}}");

                string fileName = GetFileName();
                string sourceText = _builder.ToString();
                _sourceGenerationContext.AddSource(fileName, SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));

                string GetFileName()
                {
                    return $"{operationContractSpec.Method.ToDisplayString()
                        .Replace(".", "_")
                        .Replace("<", "-")
                        .Replace(">", "-")
                        .Replace("(", "_")
                        .Replace(")", "_")
                        .Replace("[", "_")
                        .Replace("]", "_")
                        .Replace(", ", "_")
                        .Replace(" ", "_")}_OperationInvoker.g.cs";
                }
            }

            private static bool FlowsIn(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.RefKind == RefKind.In || parameterSymbol.RefKind == RefKind.Ref || parameterSymbol.RefKind == RefKind.None;
            }

            private static bool FlowOut(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.RefKind == RefKind.Out || parameterSymbol.RefKind == RefKind.Ref;
            }

            private static string GetRefKind(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    _ => string.Empty,
                };
            }
        }
    }
}
