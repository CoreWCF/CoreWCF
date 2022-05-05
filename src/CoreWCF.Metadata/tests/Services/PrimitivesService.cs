// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ServiceContract;

namespace Services
{
    public class PrimitivesService : IPrimitivesService
    {
        public bool EchoBool(bool data) => throw new NotImplementedException();
        public byte EchoByte(byte data) => throw new NotImplementedException();
        public char EchoChar(char data) => throw new NotImplementedException();
        public DateTime EchoDateTime(DateTime data) => throw new NotImplementedException();
        public decimal EchoDecimal(decimal data) => throw new NotImplementedException();
        public double EchoDouble(double data) => throw new NotImplementedException();
        public float EchoFloat(float data) => throw new NotImplementedException();
        public Guid EchoGuid(Guid data) => throw new NotImplementedException();
        public int EchoInt(int data) => throw new NotImplementedException();
        public long EchoLong(long data) => throw new NotImplementedException();
        public sbyte[] EchoSbyte(sbyte[] data) => throw new NotImplementedException();
        public short EchoShort(short data) => throw new NotImplementedException();
        public string EchoString(string data) => throw new NotImplementedException();
        public TimeSpan EchoTimeSpan(TimeSpan data) => throw new NotImplementedException();
        public uint EchoUint(uint data) => throw new NotImplementedException();
        public ulong EchoUlong(ulong data) => throw new NotImplementedException();
        public Uri EchoUri(Uri data) => throw new NotImplementedException();
        public ushort EchoUshort(ushort data) => throw new NotImplementedException();
    }
}
