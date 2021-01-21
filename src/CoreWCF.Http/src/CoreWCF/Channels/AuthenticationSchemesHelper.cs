// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace CoreWCF.Channels
{
    internal static class AuthenticationSchemesHelper
    {
        public static bool IsSet(this AuthenticationSchemes thisPtr, AuthenticationSchemes authenticationSchemes)
        {
            return (thisPtr & authenticationSchemes) == authenticationSchemes;
        }

        public static bool IsNotSet(this AuthenticationSchemes thisPtr, AuthenticationSchemes authenticationSchemes)
        {
            return (thisPtr & authenticationSchemes) == 0;
        }
    }
}
