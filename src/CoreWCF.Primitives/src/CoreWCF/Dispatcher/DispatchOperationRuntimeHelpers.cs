// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;

namespace CoreWCF.Dispatcher;

public static class DispatchOperationRuntimeHelpers
{
    internal static Dictionary<string, IOperationInvoker> OperationInvokers { get; } = new();

    public static void RegisterOperationInvoker(string key, IOperationInvoker invoker)
    {
        OperationInvokers[key] = invoker;
    }

    internal static string GetKey(MethodInfo method)
    {
        StringBuilder stringBuilder = new StringBuilder($"{method.DeclaringType.FullName}.{method.Name}(");
        stringBuilder.Append(string.Join(", ", method.GetParameters().Select(GetParameterString)));
        foreach (var keyValuePair in s_IntegratedTypesMap)
        {
            stringBuilder.Replace(keyValuePair.Key, keyValuePair.Value);
        }
        stringBuilder.Replace("+", ".");
        stringBuilder.Append(")");
        return stringBuilder.ToString();
    }

    private static string GetParameterString(ParameterInfo p)
    {
        StringBuilder sb = new StringBuilder();
        bool removeLastChar = false;
        if (p.IsOut)
        {
            sb.Append("out ");
            removeLastChar = true;
        }
        else if (p.ParameterType.IsByRef)
        {
            sb.Append("ref ");
            removeLastChar = true;
        }

        if (p.IsDefined(typeof(ParamArrayAttribute)))
        {
            sb.Append("params ");
        }

        string parameterName = GetParameterFullName(p.ParameterType);
        if (removeLastChar)
        {
            sb.Append(parameterName.Substring(0, parameterName.Length - 1));
        }
        else
        {
            sb.Append(parameterName);
        }
        return sb.ToString();
    }

    private static string GetParameterFullName(Type type)
    {
        if (type.IsGenericType)
        {
            type.GetGenericTypeDefinition();
            StringBuilder sb = new StringBuilder();
            sb.Append(type.FullName.Substring(0, type.FullName.IndexOf('`')));
            sb.Append("<");
            sb.Append(string.Join(", ", type.GetGenericArguments().Select(GetParameterFullName)));
            sb.Append(">");
            return sb.ToString();
        }

        return type.FullName;
    }

    private static Dictionary<string, string> s_IntegratedTypesMap = new Dictionary<string, string>()
    {
        { "System.Boolean", "bool" },
        { "System.Byte", "byte" },
        { "System.SByte", "sbyte" },
        { "System.Char", "char" },
        { "System.Decimal", "decimal" },
        { "System.Double", "double" },
        { "System.Single", "single" },
        { "System.Int32", "int" },
        { "System.UInt32", "uint" },
        { "System.IntPtr", "nint" },
        { "System.UIntPtr", "nuint" },
        { "System.Int64", "long" },
        { "System.UInt64", "ulong" },
        { "System.Int16", "short" },
        { "System.UInt16", "ushort" },
        { "System.Object", "object" },
        { "System.String", "string" }
    };
}
