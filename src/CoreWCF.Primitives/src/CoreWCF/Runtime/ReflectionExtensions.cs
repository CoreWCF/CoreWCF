// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

namespace CoreWCF.Runtime
{
    public static class ReflectionExtensions
    {
        public static TypeCode GetTypeCode(this Type type)
        {
            if (type == null)
                return TypeCode.Empty;

            if (type == typeof(bool))
                return TypeCode.Boolean;

            if (type == typeof(char))
                return TypeCode.Char;

            if (type == typeof(sbyte))
                return TypeCode.SByte;

            if (type == typeof(byte))
                return TypeCode.Byte;

            if (type == typeof(short))
                return TypeCode.Int16;

            if (type == typeof(ushort))
                return TypeCode.UInt16;

            if (type == typeof(int))
                return TypeCode.Int32;

            if (type == typeof(uint))
                return TypeCode.UInt32;

            if (type == typeof(long))
                return TypeCode.Int64;

            if (type == typeof(ulong))
                return TypeCode.UInt64;

            if (type == typeof(float))
                return TypeCode.Single;

            if (type == typeof(double))
                return TypeCode.Double;

            if (type == typeof(decimal))
                return TypeCode.Decimal;

            if (type == typeof(DateTime))
                return TypeCode.DateTime;

            if (type == typeof(string))
                return TypeCode.String;

            if (type.GetTypeInfo().IsEnum)
                return GetTypeCode(Enum.GetUnderlyingType(type));

            return TypeCode.Object;
        }
    }
}