// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum OperationFormatStyle
    {
        Document,
        Rpc,
    }

    static class OperationFormatStyleHelper
    {
        static public bool IsDefined(OperationFormatStyle x)
        {
            return
                x == OperationFormatStyle.Document ||
                x == OperationFormatStyle.Rpc ||
                false;
        }
    }
}