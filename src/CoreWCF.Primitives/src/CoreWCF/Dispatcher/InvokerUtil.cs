// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using CoreWCF.Description;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Dispatcher
{
    internal delegate object InvokeDelegate(object target, object[] inputs, object[] outputs);

    internal delegate IAsyncResult InvokeBeginDelegate(object target, object[] inputs, AsyncCallback asyncCallback, object state);

    internal delegate object InvokeEndDelegate(object target, object[] outputs, IAsyncResult result);

    internal delegate object CreateInstanceDelegate();

    internal static class InvokerUtil
    {
        private static readonly string s_useLegacyInvokeDelegateAppContextSwitchKey = "CoreWCF.Dispatcher.UseLegacyInvokeDelegate";
        private static readonly string s_isDynamicCodeSupportedAppContextSwitchKey = "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported";

        private static readonly Lazy<bool> s_isDynamicCodeSupported = new Lazy<bool>(() =>
            // See https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeFeature.NonNativeAot.cs,14
            AppContext.TryGetSwitch(s_isDynamicCodeSupportedAppContextSwitchKey, out bool isDynamicCodeSupported)
                ? isDynamicCodeSupported
                : true
        );

        private static readonly Lazy<bool> s_useLegacyInvokeDelegate = new Lazy<bool>(() =>
            AppContext.TryGetSwitch(s_useLegacyInvokeDelegateAppContextSwitchKey, out bool useLegacyInvokeDelegate)
                ? useLegacyInvokeDelegate
                : false
        );

        private const BindingFlags DefaultBindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
        // private readonly CriticalHelper _helper;

        //public InvokerUtil()
        //{
        //    _helper = new CriticalHelper();
        //}

        internal static bool HasDefaultConstructor(Type type)
        {
            return type.GetConstructor(DefaultBindingFlags, null, Type.EmptyTypes, null) != null;
        }

        internal static CreateInstanceDelegate GenerateCreateInstanceDelegate(Type type)
        {
            return CriticalHelper.GenerateCreateInstanceDelegate(type);
        }

        internal static InvokeDelegate GenerateInvokeDelegate(MethodInfo method, out int inputParameterCount,
            out int outputParameterCount)
        {
            return CriticalHelper.GenerateInvokeDelegate(method, out inputParameterCount, out outputParameterCount);
        }

        //internal InvokeBeginDelegate GenerateInvokeBeginDelegate(MethodInfo method, out int inputParameterCount)
        //{
        //    return helper.GenerateInvokeBeginDelegate(method, out inputParameterCount);
        //}

        //internal InvokeEndDelegate GenerateInvokeEndDelegate(MethodInfo method, out int outputParameterCount)
        //{
        //    return helper.GenerateInvokeEndDelegate(method, out outputParameterCount);
        //}

        private static class CriticalHelper
        {
            internal static CreateInstanceDelegate GenerateCreateInstanceDelegate(Type type)
            {
                if (type.GetTypeInfo().IsValueType)
                {
                    MethodInfo method = typeof(CriticalHelper).GetMethod(nameof(CreateInstanceOfStruct),
                    BindingFlags.NonPublic | BindingFlags.Static);
                    MethodInfo generic = method.MakeGenericMethod(type);
                    return (CreateInstanceDelegate)generic.CreateDelegate(typeof(CreateInstanceDelegate));
                }
                else
                {
                    MethodInfo method = typeof(CriticalHelper).GetMethod(nameof(CreateInstanceOfClass),
                    BindingFlags.NonPublic | BindingFlags.Static);
                    MethodInfo generic = method.MakeGenericMethod(type);
                    return (CreateInstanceDelegate)generic.CreateDelegate(typeof(CreateInstanceDelegate));
                }
            }

            internal static object CreateInstanceOfClass<T>() where T : class, new()
            {
                return new T();
            }

            internal static object CreateInstanceOfStruct<T>() where T : struct
            {
                return default(T);
            }

            internal static InvokeDelegate GenerateInvokeDelegate(MethodInfo method, out int inputParameterCount, out int outputParameterCount) =>
                (!s_useLegacyInvokeDelegate.Value && s_isDynamicCodeSupported.Value)
                ? GenerateInvokeDelegateInternalWithExpressions(method, out inputParameterCount, out outputParameterCount)
                : GenerateInvokeDelegateInternal(method, out inputParameterCount, out outputParameterCount);

            private static InvokeDelegate GenerateInvokeDelegateInternal(MethodInfo method, out int inputParameterCount, out int outputParameterCount)
            {
                ParameterInfo[] parameters = method.GetParameters();
                bool returnsValue = method.ReturnType != typeof(void);
                int paramCount = parameters.Length;

                var inputParamPositions = new List<int>();
                var outputParamPositions = new List<int>();
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (ServiceReflector.FlowsIn(parameters[i]))
                    {
                        inputParamPositions.Add(i);
                    }

                    if (ServiceReflector.FlowsOut(parameters[i]))
                    {
                        outputParamPositions.Add(i);
                    }
                }

                int[] inputPos = inputParamPositions.ToArray();
                int[] outputPos = outputParamPositions.ToArray();

                inputParameterCount = inputPos.Length;
                outputParameterCount = outputPos.Length;

                return delegate (object target, object[] inputs, object[] outputs)
                {
                    object[] paramsLocal = null;
                    if (paramCount > 0)
                    {
                        paramsLocal = new object[paramCount];

                        for (int i = 0; i < inputPos.Length; i++)
                        {
                            paramsLocal[inputPos[i]] = inputs[i];
                        }
                    }

                    object result = null;
                    try
                    {
                        if (returnsValue)
                        {
                            result = method.Invoke(target, paramsLocal);
                        }
                        else
                        {
                            method.Invoke(target, paramsLocal);
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    }

                    for (int i = 0; i < outputPos.Length; i++)
                    {
                        Debug.Assert(paramsLocal != null);
                        outputs[i] = paramsLocal[outputPos[i]];
                    }

                    return result;
                };
            }

            private static InvokeDelegate GenerateInvokeDelegateInternalWithExpressions(MethodInfo method, out int inputParameterCount, out int outputParameterCount)
            {
                inputParameterCount = 0;
                outputParameterCount = 0;
                ParameterInfo[] parameters = method.GetParameters();
                bool returnsValue = method.ReturnType != typeof(void);

                var targetParam = Expression.Parameter(typeof(object), "target");
                var inputsParam = Expression.Parameter(typeof(object[]), "inputs");
                var outputsParam = Expression.Parameter(typeof(object[]), "outputs");

                List<ParameterExpression> variables = new();
                var result = Expression.Variable(typeof(object), "result");
                variables.Add(result);

                List<ParameterExpression> outputVariables = new();
                List<ParameterExpression> invocationParameters = new();
                List<Expression> expressions = new();

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type variableType = parameters[i].ParameterType.IsByRef
                        ? parameters[i].ParameterType.GetElementType()
                        : parameters[i].ParameterType;
                    ParameterExpression variable = Expression.Variable(variableType, $"p{i}");

                    if (ServiceReflector.FlowsIn(parameters[i]))
                    {
                        expressions.Add(Expression.Assign(variable, Expression.Convert(Expression.ArrayIndex(inputsParam, Expression.Constant(inputParameterCount)), variableType)));
                        inputParameterCount++;
                    }

                    if (ServiceReflector.FlowsOut(parameters[i]))
                    {
                        outputParameterCount++;
                        outputVariables.Add(variable);
                    }

                    variables.Add(variable);
                    invocationParameters.Add(variable);
                }

                var castTargetParam = Expression.Convert(targetParam, method.DeclaringType);

                if (returnsValue)
                {
                    expressions.Add(Expression.Assign(result, Expression.Convert(Expression.Call(castTargetParam, method, invocationParameters), typeof(object))));
                }
                else
                {
                    expressions.Add(Expression.Call(castTargetParam, method, invocationParameters));
                    expressions.Add(Expression.Assign(result, Expression.Constant(null, typeof(object))));
                }

                int j = 0;
                foreach (var outputVariable in outputVariables)
                {
                    expressions.Add(Expression.Assign(
                        Expression.ArrayAccess(outputsParam, Expression.Constant(j)),
                        Expression.Convert(outputVariable, typeof(object))));
                    j++;
                }

                expressions.Add(result);

                BlockExpression finalBlock = Expression.Block(variables: variables, expressions: expressions);

                Expression<InvokeDelegate> lambda = Expression.Lambda<InvokeDelegate>(
                    finalBlock,
                    targetParam,
                    inputsParam,
                    outputsParam);

                //if (Logger.IsEnabled(LogLevel.Debug))
                //{
                //    var expr = GetDebugString(finalBlock);
                //    Logger.LogDebug("Generated expression for {0}.{1}:{3}{2}", method.DeclaringType, method.Name, expr, Environment.NewLine);
                //}
                
                return lambda.Compile();

                string GetDebugString(Expression expr)
                {
                    if (expr is BlockExpression block)
                    {
                        StringBuilder sb = new();
                        foreach (var e in block.Expressions)
                        {
                            sb.AppendLine(GetDebugString(e));
                        }
                        return sb.ToString();
                    }
                    else
                    {
                        return expr.ToString();
                    }
                }
            }

            //public InvokeBeginDelegate GenerateInvokeBeginDelegate(MethodInfo method, out int inputParameterCount)
            //{
            //    ParameterInfo[] parameters = method.GetParameters();
            //    var inputCount = parameters.Length;
            //    inputParameterCount = inputCount;

            //    InvokeBeginDelegate lambda =
            //        delegate(object target, object[] inputs, AsyncCallback callback, object state)
            //        {
            //            object[] inputsLocal = new object[inputCount];
            //            for (var i = 0; i < inputCount; i++)
            //            {
            //                inputsLocal[i] = inputs[i];
            //            }
            //            inputsLocal[inputCount] = callback;
            //            inputsLocal[inputCount + 1] = state;
            //            object result = method.Invoke(target, inputs);
            //            return result as IAsyncResult;
            //        };

            //    return lambda;
            //}

            //public InvokeEndDelegate GenerateInvokeEndDelegate(MethodInfo method, out int outParameterCount)
            //{

            //    InvokeEndDelegate lambda =
            //        delegate(object target, object[] outputs, IAsyncResult result)
            //        {

            //        }
            //}
        }
    }
}
