﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security;

namespace CoreWCF.Dispatcher
{
    delegate object InvokeDelegate(object target, object[] inputs, object[] outputs);
    delegate IAsyncResult InvokeBeginDelegate(object target, object[] inputs, AsyncCallback asyncCallback, object state);
    delegate object InvokeEndDelegate(object target, object[] outputs, IAsyncResult result);
    delegate object CreateInstanceDelegate();

    internal sealed class InvokerUtil
    {
        private CriticalHelper helper;

        public InvokerUtil()
        {
            helper = new CriticalHelper();
        }

        internal CreateInstanceDelegate GenerateCreateInstanceDelegate(Type type, ConstructorInfo constructor)
        {
            return helper.GenerateCreateInstanceDelegate(type, constructor);
        }

        internal InvokeDelegate GenerateInvokeDelegate(MethodInfo method, out int inputParameterCount,
            out int outputParameterCount)
        {
            return helper.GenerateInvokeDelegate(method, out inputParameterCount, out outputParameterCount);
        }

        //internal InvokeBeginDelegate GenerateInvokeBeginDelegate(MethodInfo method, out int inputParameterCount)
        //{
        //    return helper.GenerateInvokeBeginDelegate(method, out inputParameterCount);
        //}

        //internal InvokeEndDelegate GenerateInvokeEndDelegate(MethodInfo method, out int outputParameterCount)
        //{
        //    return helper.GenerateInvokeEndDelegate(method, out outputParameterCount);
        //}

        private class CriticalHelper
        {
            internal CreateInstanceDelegate GenerateCreateInstanceDelegate(Type type, ConstructorInfo constructor)
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

            internal InvokeDelegate GenerateInvokeDelegate(MethodInfo method, out int inputParameterCount, out int outputParameterCount)
            {
                ParameterInfo[] parameters = method.GetParameters();
                bool returnsValue = method.ReturnType != typeof(void);
                var inputCount = parameters.Length;
                inputParameterCount = inputCount;

                var outputParamPositions = new List<int>();
                for (int i = 0; i < inputParameterCount; i++)
                {
                    if (parameters[i].ParameterType.IsByRef)
                    {
                        outputParamPositions.Add(i);
                    }
                }

                var outputPos = outputParamPositions.ToArray();
                outputParameterCount = outputPos.Length;

                InvokeDelegate lambda = delegate (object target, object[] inputs, object[] outputs)
                {
                    object[] inputsLocal = null;
                    if (inputCount > 0)
                    {
                        inputsLocal = new object[inputCount];
                        for (var i = 0; i < inputCount; i++)
                        {
                            inputsLocal[i] = inputs[i];
                        }
                    }
                    object result = null;
                    if (returnsValue)
                    {
                        result = method.Invoke(target, inputsLocal);
                    }
                    else
                    {
                        method.Invoke(target, inputsLocal);
                    }
                    for (var i = 0; i < outputPos.Length; i++)
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