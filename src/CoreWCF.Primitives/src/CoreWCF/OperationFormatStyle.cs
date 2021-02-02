﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum OperationFormatStyle
    {
        Document,
        Rpc,
    }

    internal static class OperationFormatStyleHelper
    {
        public static bool IsDefined(OperationFormatStyle x)
        {
            return
                x == OperationFormatStyle.Document ||
                x == OperationFormatStyle.Rpc ||
                false;
        }
    }
}