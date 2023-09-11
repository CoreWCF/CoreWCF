// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal delegate object InvokeDelegate(object target, object[] inputs, object[] outputs);

    internal delegate IAsyncResult InvokeBeginDelegate(object target, object[] inputs, AsyncCallback asyncCallback, object state);

    internal delegate object InvokeEndDelegate(object target, object[] outputs, IAsyncResult result);

    internal delegate object CreateInstanceDelegate();

    internal static class InvokerUtil
    {
        private static readonly string s_isDynamicCodeSupportedAppContextSwitchKey = "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported";

        private static readonly Lazy<bool> s_isDynamicCodeSupported = new Lazy<bool>(() =>
            // See https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeFeature.NonNativeAot.cs,14
            AppContext.TryGetSwitch(s_isDynamicCodeSupportedAppContextSwitchKey, out bool isDynamicCodeSupported)
                ? isDynamicCodeSupported
                : true
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

            internal static InvokeDelegate GenerateInvokeDelegate(MethodInfo method, out int inputParameterCount, out int outputParameterCount)
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

                return s_isDynamicCodeSupported.Value
                    ? GenerateInvokeDelegateInternalWithExpressions(paramCount, returnsValue, inputPos, outputPos, method)
                    : GenerateInvokeDelegateInternal(paramCount, returnsValue, inputPos, outputPos, method);
            }

            private static InvokeDelegate GenerateInvokeDelegateInternal(int paramCount, bool returnsValue, int[] inputPos, int[] outputPos, MethodInfo method)
            {
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

            private static InvokeDelegate GenerateInvokeDelegateInternalWithExpressions(int paramCount, bool returnsValue, int[] inputPos, int[] outputPos, MethodInfo method)
            {
                var targetParam = Expression.Parameter(typeof(object), "target");
                var inputsParam = Expression.Parameter(typeof(object[]), "inputs");
                var outputsParam = Expression.Parameter(typeof(object[]), "outputs");
                var paramsLocal = Expression.Variable(typeof(object[]), "paramsLocal");
                var result = Expression.Variable(typeof(object), "result");

                var methodParam = Expression.Constant(method);

                var assignParamsLocal = Expression.Assign(paramsLocal, Expression.Condition(
                    Expression.GreaterThan(Expression.Constant(paramCount), Expression.Constant(0)),
                    Expression.NewArrayBounds(typeof(object), Expression.Constant(paramCount)),
                    Expression.Constant(null, typeof(object[]))));

                var assignParamsLocalValues = inputPos.Length == 0
                    ? Expression.Block(Expression.Empty())
                    : Expression.Block(
                        Enumerable.Range(0, inputPos.Length).Select(i =>
                            Expression.Assign(
                                Expression.ArrayAccess(paramsLocal, Expression.Constant(inputPos[i])),
                                Expression.ArrayIndex(inputsParam, Expression.Constant(i)))));

                var invokeMethod = Expression.Block(
                    Expression.Assign(result, Expression.Condition(
                        Expression.Equal(Expression.Constant(returnsValue), Expression.Constant(true)),
                        Expression.Call(methodParam, nameof(MethodInfo.Invoke), null, targetParam, paramsLocal),
                        Expression.Block(
                            Expression.Call(methodParam, nameof(MethodInfo.Invoke), null, targetParam, paramsLocal),
                            Expression.Constant(null, typeof(object))))),
                    Expression.Empty());

                var catchParameterExpr = Expression.Parameter(typeof(TargetInvocationException), "tie");
                var throwCapturedExceptionDispatchInfoExpr = Expression.Call(Expression.Call(typeof(ExceptionDispatchInfo), nameof(ExceptionDispatchInfo.Capture), null,Expression.Property(catchParameterExpr, nameof(Exception.InnerException))), nameof(ExceptionDispatchInfo.Throw), null);

                var tryCatch = Expression.TryCatch(invokeMethod,
                    Expression.Catch(catchParameterExpr,
                        Expression.Block(
                            throwCapturedExceptionDispatchInfoExpr
                        )));

                var assignOutputs =
                    outputPos.Length == 0
                    ? Expression.Block(Expression.Empty())
                    : Expression.Block(Enumerable.Range(0, outputPos.Length).Select(i =>
                        Expression.Assign(
                            Expression.ArrayAccess(outputsParam, Expression.Constant(i)),
                            Expression.ArrayAccess(paramsLocal, Expression.Constant(outputPos[i])))));

                var returnResult = Expression.Block(
                    Expression.Empty(),
                    result);

                var lambda = Expression.Lambda<InvokeDelegate>(
                    Expression.Block(
                        new[] { paramsLocal, result },
                        assignParamsLocal,
                        assignParamsLocalValues,
                        tryCatch,
                        assignOutputs,
                        returnResult),
                    targetParam,
                    inputsParam,
                    outputsParam);

                return lambda.Compile();
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
