using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF
{
    internal static class SecurityModeHelper
    {
        internal static bool IsDefined(SecurityMode value)
        {
            return (value == SecurityMode.None ||
                value == SecurityMode.Transport ||
                value == SecurityMode.Message ||
                value == SecurityMode.TransportWithMessageCredential);
        }
    }
}
