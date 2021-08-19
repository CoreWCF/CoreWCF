// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Primitives.Tests.DispatcherServer
{
    internal class ServerMethods
    {
        public static ServerMethods GetInstance()
        {
            return new ServerMethods();
        }

        public void WithoutParams()
        {
        }

        public byte WithoutParamsWithReturn()
        {
            return byte.MinValue;
        }

        public T WithoutParamsWithReturnGenericType<T>()
        {
            return default(T);
        }

        public void WithOneValueParam(byte param)
        {
        }

        public void WithOneOutParam(out byte param)
        {
            param = byte.MinValue;
        }

        public void WithOneInParam(in byte param)
        {
        }

        public void WithOneRefParam(ref byte param)
        {
            param = byte.MinValue;
        }

        public void WithOneValueAndTwoOutParam(out byte param1, byte param2, out byte param3)
        {
            param1 = byte.MinValue;
            param3 = byte.MinValue;
        }

        public void WithOneValueAndTwoRefParam(ref byte param1, byte param2, ref byte param3)
        {
            param1 = byte.MinValue;
            param3 = byte.MinValue;
        }

        public void WithOneValueAndTwoInParam(in byte param1, byte param2, in byte param3)
        {
        }

        public void WithAllTypeParam(in byte param1, byte param2, ref byte param3, out byte param4)
        {
            param3 = byte.MinValue;
            param4 = byte.MinValue;
        }
        
    }
}
