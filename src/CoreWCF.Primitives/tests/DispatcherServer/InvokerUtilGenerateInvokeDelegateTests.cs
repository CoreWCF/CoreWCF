// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;

using CoreWCF.Dispatcher;
using CoreWCF.Primitives.Tests.DispatcherServer;

using Xunit;

namespace DispatcherServerTests
{
    public class InvokerUtilGenerateInvokeDelegateTests
    {
        [Fact]
        public void MethodWithoutParams()
        {
            // void WithoutParams()
            MethodInfo withoutParamsMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithoutParams));

            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withoutParamsMethod);

            Assert.Equal(0, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(0, invokeDelegateInfo.OutputParameterCount);
            Assert.Empty(invokeDelegateInfo.OutputParameters);
        }

        [Fact]
        public void MethodWithoutParamsWithReturn()
        {
            //  byte WithoutParamsWithReturn()
            MethodInfo withoutParamsWithReturnMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithoutParamsWithReturn));

            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withoutParamsWithReturnMethod);

            Assert.Equal(0, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(0, invokeDelegateInfo.OutputParameterCount);
            Assert.Equal(byte.MinValue, invokeDelegateInfo.Result);
            Assert.Empty(invokeDelegateInfo.OutputParameters);
        }

        [Fact]
        public void MethodWithoutParamsWithReturnGenericType()
        {
            // T WithoutParamsWithReturnGenericType<T>()
            MethodInfo withoutParamsWithReturnGenericTypeMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithoutParamsWithReturnGenericType));

            Assert.ThrowsAny<InvalidOperationException>(() => GenerateInvokeDelegate(withoutParamsWithReturnGenericTypeMethod));

        }

        [Fact]
        public void MethodWithOneValueParam()
        {
            MethodInfo withOneParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneValueParam));
            // void WithOneValueParam(byte param)
            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneParamMethod, byte.MaxValue);

            Assert.Equal(1, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(0, invokeDelegateInfo.OutputParameterCount);
            Assert.Empty(invokeDelegateInfo.OutputParameters);
        }

        [Fact]
        public void MethodWithOneOutParam()
        {
            // void WithOneOutParam(out byte param)
            MethodInfo withOneOutParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneOutParam));

            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneOutParamMethod, byte.MaxValue);

            Assert.Equal(0, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(1, invokeDelegateInfo.OutputParameterCount);
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[0]);
        }

        [Fact]
        public void MethodWithOneRefParam()
        {
            // void WithOneRefParam(ref byte param)
            MethodInfo withOneRefParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneRefParam));

            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneRefParamMethod, byte.MaxValue);

            // ref is input and output type at the same time
            Assert.Equal(1, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(1, invokeDelegateInfo.OutputParameterCount);
            // WithOneRefParam set ref-param to byte.MinValue
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[0]);
        }

        [Fact]
        public void MethodWithOneInParam()
        {
            MethodInfo withOneInParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneInParam));

            // void WithOneInParam(in byte param)
            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneInParamMethod, byte.MaxValue);

            // as ref param
            Assert.Equal(1, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(1, invokeDelegateInfo.OutputParameterCount);
            // in parameters doesn`t set up
            Assert.Equal(byte.MaxValue, invokeDelegateInfo.OutputParameters[0]);
        }

        [Fact]
        public void MethodWithOneValueAndTwoOutParam()
        {
            MethodInfo withOneValueAndTwoOutParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneValueAndTwoOutParam));

            //void WithOneValueAndTwoOutParam(out byte param1, byte param2, out byte param3)
            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneValueAndTwoOutParamMethod, byte.MaxValue, byte.MaxValue, byte.MaxValue);

            Assert.Equal(1, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(2, invokeDelegateInfo.OutputParameterCount);
            // WithOneValueAndOutParam set out-param to byte.MinValue
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[0]);
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[1]);
        }


        [Fact]
        public void MethodWithOneValueAndTwoRefParam()
        {
            MethodInfo withOneValueAndTwoRefParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneValueAndTwoRefParam));

            //void WithOneValueAndTwoRefParam(ref byte param1, byte param2, ref byte param3)
            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneValueAndTwoRefParamMethod, byte.MaxValue, byte.MaxValue, byte.MaxValue);

            // ref is input and output type at the same time
            Assert.Equal(3, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(2, invokeDelegateInfo.OutputParameterCount);
            // WithOneValueAndTwoRefParam set ref-param to byte.MinValue
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[0]);
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[1]);
        }

        [Fact]
        public void MethodWithOneValueAndTwoInParam()
        {
            MethodInfo withOneValueAndTwoInParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithOneValueAndTwoInParam));

            //void WithOneValueAndTwoInParam(in byte param1, byte param2, in byte param3)
            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withOneValueAndTwoInParamMethod, byte.MaxValue, byte.MaxValue, byte.MaxValue);

            // as ref param
            Assert.Equal(3, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(2, invokeDelegateInfo.OutputParameterCount);
            // in parameters doesn`t set up
            Assert.Equal(byte.MaxValue, invokeDelegateInfo.OutputParameters[0]);
            Assert.Equal(byte.MaxValue, invokeDelegateInfo.OutputParameters[1]);
        }

        [Fact]
        public void MethodWithAllTypeParam()
        {
            MethodInfo withAllTypeParamMethod = typeof(ServerMethods).GetMethod(nameof(ServerMethods.WithAllTypeParam));

            //void WithAllTypeParam(in byte param1, byte param2, ref byte param3, out byte param4)
            InvokeDelegateInfo invokeDelegateInfo = GenerateInvokeDelegate(withAllTypeParamMethod, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

            // ref and in are input and output types at the same time
            Assert.Equal(3, invokeDelegateInfo.InputParameterCount);
            Assert.Equal(3, invokeDelegateInfo.OutputParameterCount);
            // WithAllTypeParam set ref and out-param to byte.MinValue
            // in parameters doesn`t set up
            Assert.Equal(byte.MaxValue, invokeDelegateInfo.OutputParameters[0]);
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[1]);
            Assert.Equal(byte.MinValue, invokeDelegateInfo.OutputParameters[2]);
        }

        private InvokeDelegateInfo GenerateInvokeDelegate(MethodInfo method, params object[] inputParameters)
        {
            InvokeDelegate invokeDelegate = InvokerUtil.GenerateInvokeDelegate(method, out int inputParameterCount, out int outputParameterCount);

            object[] outputParameters = new object[outputParameterCount];
            object result = invokeDelegate(ServerMethods.GetInstance(), inputParameters, outputParameters);

            return new InvokeDelegateInfo
            {
                Result = result,
                InputParameterCount = inputParameterCount,
                OutputParameterCount = outputParameterCount,
                OutputParameters = outputParameters
            };
        }

        internal class InvokeDelegateInfo
        {
            public object Result;
            public int InputParameterCount;
            public int OutputParameterCount;
            public object[] OutputParameters;
        }
    }
}
