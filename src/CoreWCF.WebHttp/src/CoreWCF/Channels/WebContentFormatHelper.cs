// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal static class WebContentFormatHelper
    {
        internal static bool IsDefined(WebContentFormat format)
        {
            return (format == WebContentFormat.Default
                || format == WebContentFormat.Xml
                || format == WebContentFormat.Json
                || format == WebContentFormat.Raw);
        }
    }
}
