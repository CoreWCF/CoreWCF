// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// The target framework this test assembly was built for, used to select per-framework golden
    /// fixture overrides.
    /// </summary>
    /// <remarks>
    /// Read from an assembly attribute injected by the project file rather than from an #if ladder.
    /// CoreWCF.Metadata's WsdlHelper already had to duplicate its #if NET10_0_OR_GREATER block four
    /// times in one file; this scales to new target frameworks with no code change at all.
    /// </remarks>
    internal static class TargetFrameworkInfo
    {
        /// <summary>
        /// The framework whose output is the canonical baseline. Chosen as the lowest common
        /// target framework: it is present in every CI leg and in the default local set, so a
        /// developer without a preview SDK still regenerates the canonical file.
        /// </summary>
        public const string BaselineTargetFramework = "net8.0";

        private static readonly string s_current = ReadTargetFramework();

        public static string Current => s_current;

        public static bool IsBaseline =>
            string.Equals(s_current, BaselineTargetFramework, StringComparison.Ordinal);

        private static string ReadTargetFramework()
        {
            Assembly assembly = typeof(TargetFrameworkInfo).Assembly;
            foreach (AssemblyMetadataAttribute attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(attribute.Key, "TargetFramework", StringComparison.Ordinal))
                {
                    return attribute.Value;
                }
            }

            throw new InvalidOperationException(
                "The test assembly is missing its TargetFramework AssemblyMetadataAttribute. It is injected by " +
                "CoreWCF.DataContractSerialization.Tests.csproj and is required to resolve per-framework fixtures.");
        }
    }
}
