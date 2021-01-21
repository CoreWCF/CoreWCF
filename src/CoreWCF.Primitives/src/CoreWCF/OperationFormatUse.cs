// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum OperationFormatUse
    {
        Literal,
        Encoded,
    }

    internal static class OperationFormatUseHelper
    {
        static public bool IsDefined(OperationFormatUse x)
        {
            return
                x == OperationFormatUse.Literal ||
                x == OperationFormatUse.Encoded ||
                false;
        }
    }
}