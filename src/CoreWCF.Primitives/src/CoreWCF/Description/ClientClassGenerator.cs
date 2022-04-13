// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    internal static class ClientClassGenerator
    {
        internal static string GetClientClassName(string interfaceName)
        {
            return GetClassName(interfaceName) + Strings.ClientTypeSuffix;
        }

        private static string GetClassName(string interfaceName)
        {
            // maybe strip a leading 'I'
            if (interfaceName.Length >= 2 &&
                string.Compare(interfaceName, 0, Strings.InterfaceTypePrefix, 0, Strings.InterfaceTypePrefix.Length, StringComparison.Ordinal) == 0 &&
                char.IsUpper(interfaceName, 1))
                return interfaceName.Substring(1);
            else
                return interfaceName;
        }

        private static class Strings
        {
            public const string ClientBaseChannelProperty = "Channel";
            public const string ClientTypeSuffix = "Client";
            public const string InterfaceTypePrefix = "I";
        }
    }
}
