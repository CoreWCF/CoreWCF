﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum AddressFilterMode
    {
        Exact, // AddressFilterMode.default
        Prefix,
        Any,
    }

    internal static class AddressFilterModeHelper
    {
        public static bool IsDefined(AddressFilterMode x)
        {
            return
                x == AddressFilterMode.Exact ||
                x == AddressFilterMode.Prefix ||
                x == AddressFilterMode.Any;
        }
    }
}