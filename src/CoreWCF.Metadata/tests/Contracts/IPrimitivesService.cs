// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = "PrimitivesService")]
    internal interface IPrimitivesService
    {
        [OperationContract]
        Uri EchoUri(Uri data);

        [OperationContract]
        sbyte[] EchoSbyte(sbyte[] data);

        [OperationContract]
        bool EchoBool(bool data);

        [OperationContract]
        DateTime EchoDateTime(DateTime data);

        [OperationContract]
        decimal EchoDecimal(decimal data);

        [OperationContract]
        double EchoDouble(double data);

        [OperationContract]
        float EchoFloat(float data);

        [OperationContract]
        int EchoInt(int data);

        [OperationContract]
        long EchoLong(long data);

        [OperationContract]
        short EchoShort(short data);

        [OperationContract]
        string EchoString(string data);

        [OperationContract]
        byte EchoByte(byte data);

        [OperationContract]
        uint EchoUint(uint data);

        [OperationContract]
        ulong EchoUlong(ulong data);

        [OperationContract]
        ushort EchoUshort(ushort data);

        [OperationContract]
        char EchoChar(char data);

        [OperationContract]
        TimeSpan EchoTimeSpan(TimeSpan data);

        [OperationContract]
        Guid EchoGuid(Guid data);
    }
}
