// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

namespace CoreWCF.Configuration
{
    internal class ServiceReflector
    {
        internal static Type ResolveTypeFromName(string name)
        {
            //First check if present in loaded assemblies
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(name) is Type found)
                {
                    return found;
                }
            }

            //If not present in loaded assemblies, AssemblyQualifiedName  should be "FooNameSpace.BarInterface,FooNameSpace".
            return Type.GetType(name, true);
        }
    }
}
