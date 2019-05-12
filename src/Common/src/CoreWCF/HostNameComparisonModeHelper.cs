﻿using CoreWCF;
using System.ComponentModel;

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
