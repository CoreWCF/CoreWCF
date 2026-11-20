// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// On .NET Core / .NET 5+ the runtime honors this attribute when applied to an assembly
    /// to bypass member-accessibility checks against the named target assembly. Used by the
    /// runtime-emitted proxy types in <see cref="Helpers.Interceptor.InterceptorRuntimeProxies"/>
    /// to derive from internal interfaces of <c>System.ServiceModel.Primitives</c>.
    ///
    /// .NET Framework 4.x ignores this attribute. Tests that depend on it should be marked
    /// <c>[NetCoreOnlyFact]</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
