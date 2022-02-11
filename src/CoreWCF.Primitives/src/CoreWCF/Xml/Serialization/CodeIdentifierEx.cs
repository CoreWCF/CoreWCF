// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using CoreWCF.Runtime;

namespace CoreWCF.Xml.Serialization
{
    internal class CodeIdentifierEx
    {
        internal static Func<string, string> GetCSharpName { get; private set; } = GetCSharpNameStub;
        internal static Func<Type, string> GetCSharpNameByType { get; private set; } = GetCSharpNameByTypeStub;

        private static string GetCSharpNameStub(string name)
        {
            var getCSharpNameMethod = typeof(CodeIdentifier).GetMethod("GetCSharpName", new Type[] { typeof(string) });
            Fx.Assert(getCSharpNameMethod != null, "Missing method CodeIdentifier.GetCSharpName(string)");
            GetCSharpName = (Func<string, string>)getCSharpNameMethod.CreateDelegate(typeof(Func<string, string>));
            return GetCSharpName(name);
        }

        private static string GetCSharpNameByTypeStub(Type type)
        {
            var getCSharpNameMethod = typeof(CodeIdentifier).GetMethod("GetCSharpName", new Type[] { typeof(Type) });
            Fx.Assert(getCSharpNameMethod != null, "Missing method CodeIdentifier.GetCSharpName(Type)");
            GetCSharpNameByType = (Func<Type, string>)getCSharpNameMethod.CreateDelegate(typeof(Func<Type, string>));
            return GetCSharpNameByType(type);
        }
    }
}
