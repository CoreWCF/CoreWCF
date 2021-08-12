// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using CoreWCF;

namespace CoreWCF
{
    internal static class HostNameComparisonModeHelper
    {
        internal static bool IsDefined(HostNameComparisonMode value)
        {
            return
                value == HostNameComparisonMode.StrongWildcard
                || value == HostNameComparisonMode.Exact
                || value == HostNameComparisonMode.WeakWildcard;
        }

        public static void Validate(HostNameComparisonMode value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(HostNameComparisonMode)));
            }
        }
    }
}
