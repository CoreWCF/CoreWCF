// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace CoreWCF.Dispatcher
{
    internal delegate object InvokeDelegate(object target, object[] inputs, object[] outputs);

    internal delegate IAsyncResult InvokeBeginDelegate(object target, object[] inputs, AsyncCallback asyncCallback, object state);

    internal delegate object InvokeEndDelegate(object target, object[] outputs, IAsyncResult result);

    internal delegate object CreateInstanceDelegate();

    internal static class InvokerUtil
    {
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
                int inputCount = parameters.Length;
                inputParameterCount = inputCount;

                var outputParamPositions = new List<int>();
                for (int i = 0; i < inputParameterCount; i++)
                {
                    if (parameters[i].ParameterType.IsByRef)
                    {
                        outputParamPositions.Add(i);
                    }
                }

                int[] outputPos = outputParamPositions.ToArray();
                outputParameterCount = outputPos.Length;

                // TODO: Replace with expression to remove performance cost of calling delegate.Invoke.
                InvokeDelegate lambda = delegate (object target, object[] inputs, object[] outputs)
                {
                    object[] inputsLocal = null;
                    if (inputCount > 0)
                    {
                        inputsLocal = new object[inputCount];
                        for (int i = 0; i < inputCount; i++)
                        {
                            inputsLocal[i] = inputs[i];
                        }
                    }

                    object result = null;
                    try
                    {
                        if (returnsValue)
                        {
                            result = method.Invoke(target, inputsLocal);
                        }
                        else
                        {
                            method.Invoke(target, inputsLocal);
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    }

                    for (int i = 0; i < outputPos.Length; i++)
                    {
                        outputs[i] = inputs[outputPos[i]];
                    }

                    return result;
                };

                return lambda;
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
